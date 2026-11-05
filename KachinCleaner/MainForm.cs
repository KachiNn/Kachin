using System.ComponentModel;

namespace KachinCleaner;

internal sealed class MainForm : Form
{
    private readonly DataGridView grid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, RowHeadersVisible = false };
    private readonly Label status = new() { AutoSize = true, Text = "Review temporary-file groups before deletion." };
    private readonly TextBox issues = new() { Dock = DockStyle.Bottom, Multiline = true, ReadOnly = true, Height = 88, ScrollBars = ScrollBars.Vertical };
    private readonly Button scanButton = new() { Text = "Scan" };
    private readonly Button deleteButton = new() { Text = "Delete selected groups", Enabled = false };
    private ScanResult? lastScan;

    public MainForm()
    {
        Text = "Kachin Cleaner"; Width = 720; Height = 480; MinimumSize = new Size(560, 360); StartPosition = FormStartPosition.CenterScreen;
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8), WrapContents = false };
        top.Controls.Add(scanButton); top.Controls.Add(deleteButton); top.Controls.Add(status);
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "Clean", Width = 55 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Group", HeaderText = "Approved temporary location", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Count", HeaderText = "Files", Width = 80, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Size", HeaderText = "Size", Width = 110, ReadOnly = true });
        Controls.Add(grid); Controls.Add(issues); Controls.Add(top);
        scanButton.Click += async (_, _) => await ScanAsync();
        deleteButton.Click += (_, _) => DeleteSelected();
    }

    private async Task ScanAsync()
    {
        scanButton.Enabled = deleteButton.Enabled = false; grid.Rows.Clear(); issues.Clear(); status.Text = "Scanning approved locations...";
        var progress = new Progress<string>(text => status.Text = text);
        try
        {
            lastScan = await TempScanner.ScanAsync(progress, CancellationToken.None);
            foreach (var grouping in lastScan.Files.GroupBy(f => f.Group))
                grid.Rows.Add(false, grouping.Key, grouping.Count(), FormatSize(grouping.Sum(f => f.Bytes)));
            issues.Lines = lastScan.Issues.Take(300).ToArray();
            status.Text = $"Review: {lastScan.Files.Count:N0} files, {FormatSize(lastScan.Files.Sum(f => f.Bytes))}. Scan did not change files.";
            deleteButton.Enabled = lastScan.Files.Count > 0;
        }
        catch (Exception ex)
        {
            status.Text = "Scan stopped; no files were changed."; issues.Text = ex.Message;
        }
        finally { scanButton.Enabled = true; }
    }

    private void DeleteSelected()
    {
        if (lastScan is null) return;
        var groups = grid.Rows.Cast<DataGridViewRow>().Where(r => Convert.ToBoolean(r.Cells[0].Value)).Select(r => Convert.ToString(r.Cells[1].Value)!).ToHashSet();
        if (groups.Count == 0) { MessageBox.Show(this, "Select at least one group.", "Nothing selected", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var selected = lastScan.Files.Where(f => groups.Contains(f.Group)).ToArray();
        var bytes = selected.Sum(f => f.Bytes);
        if (MessageBox.Show(this, $"Delete {selected.Length:N0} files ({FormatSize(bytes)}) from the selected temporary groups? This cannot be undone.", "Confirm deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var result = TempScanner.Delete(selected);
        status.Text = $"Deleted {result.Deleted:N0} of {selected.Length:N0} selected files.";
        issues.Lines = result.Issues.Take(300).ToArray();
        lastScan = null; deleteButton.Enabled = false;
        MessageBox.Show(this, "Deletion finished. Scan again to review the current contents.", "Finished", MessageBoxButtons.OK, result.Issues.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private static string FormatSize(long bytes) => bytes < 1024 ? $"{bytes} B" : bytes < 1024 * 1024 ? $"{bytes / 1024d:N1} KB" : bytes < 1024L * 1024 * 1024 ? $"{bytes / 1024d / 1024:N1} MB" : $"{bytes / 1024d / 1024 / 1024:N1} GB";
}

using System.ComponentModel;
using System.Drawing;

namespace KachinCleaner;

internal sealed class MainForm : Form
{
    private readonly ComboBox language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 128 };
    private readonly ListBox rootsList = new() { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(250, 252, 252), ForeColor = Color.FromArgb(38, 57, 61), Font = new Font("Segoe UI", 9.5f), ItemHeight = 25 };
    private readonly DataGridView grid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, GridColor = Color.FromArgb(232, 238, 237), EnableHeadersVisualStyles = false, AllowUserToResizeRows = false };
    private readonly Label titleLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 21, FontStyle.Bold), ForeColor = Color.White };
    private readonly Label subtitleLabel = new() { AutoSize = true, ForeColor = Color.FromArgb(211, 239, 234) };
    private readonly Label rootsLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.FromArgb(30, 54, 57), Margin = new Padding(0, 0, 0, 4) };
    private readonly Label resultsLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.FromArgb(30, 54, 57), Margin = new Padding(0, 0, 0, 4) };
    private readonly Label status = new() { AutoSize = true, ForeColor = Color.FromArgb(53, 91, 87), Font = new Font("Segoe UI", 9, FontStyle.Bold), Margin = new Padding(12, 5, 4, 0) };
    private readonly Label summary = new() { AutoSize = true, ForeColor = Color.FromArgb(85, 103, 103), Margin = new Padding(8, 5, 4, 0) };
    private readonly Button addButton = new();
    private readonly Button removeButton = new();
    private readonly Button scanButton = new();
    private readonly Button deleteButton = new();
    private readonly CheckBox selectAll = new() { AutoSize = true };
    private readonly TextBox issues = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(250, 252, 251), ForeColor = Color.FromArgb(80, 96, 96), Font = new Font("Segoe UI", 9) };
    private readonly Label issuesLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(92, 111, 109) };
    private bool chinese = true;
    private ScanResult? lastScan;
    private bool scanning;
    private bool deletionFinished;
    private bool synchronizingSelection;
    private string? progressMessage;
    private string[] rawIssues = Array.Empty<string>();

    public MainForm()
    {
        Text = "Kachin Cleaner";
        MinimumSize = new Size(800, 600);
        Size = new Size(1100, 760);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(239, 245, 243);
        Font = new Font("Segoe UI", 9.5f);
        ForeColor = Color.FromArgb(33, 49, 52);

        language.Items.AddRange(new object[] { "中文", "English" });
        language.SelectedIndex = 0;
        language.SelectedIndexChanged += (_, _) => { chinese = language.SelectedIndex == 0; ApplyLanguage(); };
        addButton.Click += (_, _) => AddRoot();
        removeButton.Click += (_, _) => RemoveRoot();
        scanButton.Click += async (_, _) => await ScanAsync();
        deleteButton.Click += (_, _) => DeleteSelected();
        selectAll.CheckedChanged += (_, _) =>
        {
            if (synchronizingSelection) return;
            synchronizingSelection = true;
            try
            {
                foreach (DataGridViewRow row in grid.Rows) row.Cells[0].Value = selectAll.Checked;
            }
            finally { synchronizingSelection = false; }
            UpdateDeleteEnabled();
        };

        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(246, 249, 248);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(54, 77, 76);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        grid.ColumnHeadersHeight = 40;
        grid.RowTemplate.Height = 35;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 241, 234);
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(29, 67, 58);
        grid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", Width = 48, ReadOnly = false, ThreeState = false });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Path", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 220, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Size", Width = 120, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.CellValueChanged += (_, e) =>
        {
            if (e.ColumnIndex != 0) return;
            if (!synchronizingSelection) SynchronizeSelectAll();
            UpdateDeleteEnabled();
        };
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };

        var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = 112, Padding = new Padding(28, 16, 28, 14), ColumnCount = 2, RowCount = 2, BackColor = Color.FromArgb(16, 111, 101) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(titleLabel, 0, 0);
        header.Controls.Add(subtitleLabel, 0, 1);
        header.Controls.Add(language, 1, 0);
        header.SetRowSpan(language, 2);

        language.FlatStyle = FlatStyle.Flat;
        language.BackColor = Color.FromArgb(233, 246, 242);
        language.ForeColor = Color.FromArgb(30, 76, 69);
        language.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        language.Margin = new Padding(8, 8, 0, 0);
        var rootPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(22, 16, 22, 14) };
        rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 205));
        rootPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var rootCard = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16, 12, 16, 10), Margin = new Padding(0, 0, 0, 12) };
        var rootSection = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.White };
        rootSection.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        rootSection.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rootSection.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        rootSection.Controls.Add(rootsLabel, 0, 0);
        rootSection.Controls.Add(rootsList, 0, 1);
        var rootButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
        rootButtons.Controls.Add(addButton);
        rootButtons.Controls.Add(removeButton);
        rootSection.Controls.Add(rootButtons, 0, 2);

        var resultsSection = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        resultsSection.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        resultsSection.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        resultsSection.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
        resultsSection.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        var resultHeading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        resultHeading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        resultHeading.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        resultHeading.Controls.Add(resultsLabel, 0, 0);
        resultHeading.Controls.Add(selectAll, 1, 0);
        resultsSection.Controls.Add(resultHeading, 0, 0);
        resultsSection.Controls.Add(grid, 0, 1);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Padding = new Padding(0, 4, 0, 0) };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actions.Controls.Add(scanButton, 0, 0);
        actions.Controls.Add(status, 1, 0);
        actions.Controls.Add(summary, 2, 0);
        actions.Controls.Add(deleteButton, 3, 0);
        resultsSection.Controls.Add(actions, 0, 2);
        var issueSection = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        issueSection.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        issueSection.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        issueSection.Controls.Add(issuesLabel, 0, 0);
        issueSection.Controls.Add(issues, 0, 1);
        resultsSection.Controls.Add(issueSection, 0, 3);
        rootCard.Controls.Add(rootSection);
        var resultsCard = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16, 12, 16, 12) };
        resultsCard.Controls.Add(resultsSection);
        rootPanel.Controls.Add(rootCard, 0, 0);
        rootPanel.Controls.Add(resultsCard, 0, 1);
        Controls.Add(rootPanel);
        Controls.Add(header);

        StyleButton(addButton, false);
        StyleButton(removeButton, false);
        StyleButton(scanButton, true);
        StyleButton(deleteButton, true);
        var defaultRoot = TempScanner.GetDefaultTempRoot(out var issue);
        if (!string.IsNullOrWhiteSpace(defaultRoot)) rootsList.Items.Add(defaultRoot);
        if (!string.IsNullOrWhiteSpace(issue))
        {
            rawIssues = new[] { issue };
            issues.Lines = rawIssues.Select(LocalizeIssue).ToArray();
        }
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        Text = chinese ? "Kachin Cleaner | 临时文件清理" : "Kachin Cleaner | Temporary File Cleanup";
        titleLabel.Text = chinese ? "Kachin Cleaner" : "Kachin Cleaner";
        subtitleLabel.Text = chinese ? "检查临时文件，再明确选择要删除的内容" : "Review temporary files, then choose exactly what to remove";
        rootsLabel.Text = chinese ? "扫描位置" : "Scan locations";
        resultsLabel.Text = chinese ? "扫描结果" : "Scan results";
        addButton.Text = chinese ? "+  添加文件夹" : "+  Add folder";
        removeButton.Text = chinese ? "移除所选位置" : "Remove location";
        scanButton.Text = chinese ? "开始扫描" : "Scan";
        deleteButton.Text = chinese ? "删除勾选文件" : "Delete checked files";
        selectAll.Text = chinese ? "全选文件" : "Select all files";
        issuesLabel.Text = chinese ? "问题与跳过项目" : "Issues and skipped items";
        status.Text = scanning ? (progressMessage is null ? (chinese ? "正在扫描..." : "Scanning...") : LocalizeProgress(progressMessage)) : deletionFinished ? (chinese ? "删除操作已结束。" : "Deletion finished.") : lastScan is null ? (chinese ? "扫描不会更改文件。" : "Scanning does not change files.") : (chinese ? "扫描完成；未更改任何文件。" : "Scan complete; no files were changed.");
        issues.Lines = rawIssues.Select(LocalizeIssue).ToArray();
        summary.Text = lastScan is null ? (chinese ? "尚未扫描" : "Not scanned") : chinese ? $"共 {lastScan.Files.Count:N0} 个文件 · {FormatSize(lastScan.Files.Sum(f => f.Bytes))} · 问题 {lastScan.Issues.Count:N0} 项" : $"{lastScan.Files.Count:N0} files · {FormatSize(lastScan.Files.Sum(f => f.Bytes))} · {lastScan.Issues.Count:N0} issues";
        grid.Columns[0].HeaderText = chinese ? "选择" : "Keep?";
        grid.Columns[1].HeaderText = chinese ? "完整文件路径" : "Full file path";
        grid.Columns[2].HeaderText = chinese ? "大小" : "Size";
        UpdateDeleteEnabled();
    }

    private static void StyleButton(Button button, bool primary)
    {
        button.AutoSize = true;
        button.Height = 34;
        button.Padding = new Padding(12, 3, 12, 3);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(216, 228, 224);
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(12, 132, 119) : Color.FromArgb(241, 248, 245);
        button.BackColor = primary ? Color.FromArgb(17, 119, 105) : Color.FromArgb(248, 251, 250);
        button.ForeColor = primary ? Color.White : Color.FromArgb(39, 75, 67);
        button.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        button.Margin = new Padding(0, 0, 9, 0);
    }

    private void AddRoot()
    {
        using var dialog = new FolderBrowserDialog { Description = chinese ? "选择要扫描的临时文件夹" : "Choose a temporary folder to scan", UseDescriptionForTitle = true, ShowNewFolderButton = false };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (!TempScanner.ValidateSelectableRoot(dialog.SelectedPath, out var issue))
        {
            MessageBox.Show(this, LocalizeIssue(issue), chinese ? "位置不可用" : "Location unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var full = Path.GetFullPath(dialog.SelectedPath);
        if (!rootsList.Items.Cast<string>().Any(p => string.Equals(Path.GetFullPath(p), full, StringComparison.OrdinalIgnoreCase))) rootsList.Items.Add(full);
    }

    private void RemoveRoot()
    {
        if (rootsList.SelectedIndex >= 0) rootsList.Items.RemoveAt(rootsList.SelectedIndex);
    }

    private async Task ScanAsync()
    {
        var scanRoots = rootsList.Items.Cast<string>().ToArray();
        if (scanRoots.Length == 0)
        {
            MessageBox.Show(this, chinese ? "请先添加至少一个有效扫描位置。" : "Add at least one valid scan location first.", chinese ? "没有扫描位置" : "No scan locations", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        scanning = true;
        scanButton.Enabled = deleteButton.Enabled = addButton.Enabled = removeButton.Enabled = false;
        grid.Rows.Clear();
        issues.Clear();
        rawIssues = Array.Empty<string>();
        selectAll.Checked = false;
        progressMessage = null;
        status.Text = chinese ? "正在扫描..." : "Scanning...";
        var progress = new Progress<string>(text => { progressMessage = text; status.Text = LocalizeProgress(text); });
        try
        {
            lastScan = await TempScanner.ScanAsync(scanRoots, progress, CancellationToken.None);
            foreach (var file in lastScan.Files)
            {
                var rowIndex = grid.Rows.Add(false, file.FullPath, FormatSize(file.Bytes));
                grid.Rows[rowIndex].Cells[1].ToolTipText = file.FullPath;
            }
            rawIssues = lastScan.Issues.Take(300).ToArray();
            issues.Lines = rawIssues.Select(LocalizeIssue).ToArray();
            summary.Text = chinese
                ? $"共 {lastScan.Files.Count:N0} 个文件 · {FormatSize(lastScan.Files.Sum(f => f.Bytes))} · 问题 {lastScan.Issues.Count:N0} 项"
                : $"{lastScan.Files.Count:N0} files · {FormatSize(lastScan.Files.Sum(f => f.Bytes))} · {lastScan.Issues.Count:N0} issues";
            status.Text = chinese ? "扫描完成；未更改任何文件。" : "Scan complete; no files were changed.";
            UpdateDeleteEnabled();
        }
        catch (Exception ex)
        {
            lastScan = null;
            summary.Text = chinese ? "扫描未完成" : "Scan did not complete";
            status.Text = chinese ? "扫描失败；未更改文件。" : "Scan failed; no files were changed.";
            rawIssues = new[] { ex.Message };
            issues.Lines = rawIssues.Select(LocalizeIssue).ToArray();
        }
        finally { scanning = false; scanButton.Enabled = addButton.Enabled = removeButton.Enabled = true; }
    }

    private void SynchronizeSelectAll()
    {
        var allSelected = grid.Rows.Count > 0 && grid.Rows.Cast<DataGridViewRow>().All(r => Convert.ToBoolean(r.Cells[0].Value ?? false));
        if (selectAll.Checked == allSelected) return;
        synchronizingSelection = true;
        try { selectAll.Checked = allSelected; }
        finally { synchronizingSelection = false; }
    }

    private string LocalizeProgress(string text)
    {
        if (!chinese || !text.StartsWith("Scanning ", StringComparison.Ordinal)) return text;
        var separator = text.LastIndexOf(": ", StringComparison.Ordinal);
        if (separator < 0) return "正在扫描：" + text["Scanning ".Length..];
        return $"正在扫描 {text["Scanning ".Length..separator]}：已发现 {text[(separator + 2)..]}";
    }

    private string LocalizeIssue(string text)
    {
        if (!chinese) return text;
        string Translate(string prefix, string replacement) => text.StartsWith(prefix, StringComparison.Ordinal) ? replacement + text[prefix.Length..] : text;
        return Translate("Skipped inaccessible directory '", "已跳过无法访问的目录 '")
            is var translated && translated != text ? translated
            : Translate("Skipped inaccessible or in-use item '", "已跳过无法访问或正在使用的项目 '") is var item && item != text ? item
            : Translate("Could not delete '", "无法删除 '") is var deletion && deletion != text ? deletion
            : Translate("Refused candidate outside approved folders: ", "已拒绝位于批准文件夹之外的文件：") is var outside && outside != text ? outside
            : Translate("Refused candidate because its approved folder is no longer valid: ", "已拒绝该文件，因为其批准文件夹已无效：") is var invalid && invalid != text ? invalid
            : Translate("Refused protected or reparse-point candidate: ", "已拒绝受保护或包含重解析点的文件：") is var protectedPath && protectedPath != text ? protectedPath
            : Translate("Refused changed item; only ordinary candidate files can be deleted: ", "已拒绝已变化的项目；只能删除普通候选文件：") is var changed && changed != text ? changed
            : Translate("Local application data path is unavailable.", "本地应用程序数据路径不可用。")
            is var defaultPath && defaultPath != text ? defaultPath
            : Translate("Local application data Temp is not on C:, so it was not approved.", "本地应用程序 Temp 不在 C: 盘，因此未获批准。")
            is var drive && drive != text ? drive
            : Translate("Local application data Temp does not exist: ", "本地应用程序 Temp 不存在：") is var missingTemp && missingTemp != text ? missingTemp
            : Translate("Local application data Temp contains a reparse-point path component: ", "本地应用程序 Temp 的路径组件包含重解析点：") is var reparseTemp && reparseTemp != text ? reparseTemp
            : Translate("Local application data Temp could not be validated: ", "无法验证本地应用程序 Temp：") is var validateTemp && validateTemp != text ? validateTemp
            : Translate("Select a folder path.", "请选择文件夹路径。") is var selectPath && selectPath != text ? selectPath
            : Translate("Drive roots cannot be selected. Choose an ordinary folder.", "不能选择驱动器根目录。请选择普通文件夹。") is var driveRoot && driveRoot != text ? driveRoot
            : Translate("Selected folder does not exist or is not a directory: ", "所选文件夹不存在或不是目录：") is var folder && folder != text ? folder
            : Translate("Protected Windows, Program Files, or user-profile path cannot be selected: ", "不能选择受保护的 Windows、Program Files 或用户配置文件路径：") is var protectedFolder && protectedFolder != text ? protectedFolder
            : Translate("Selected folder or one of its path components is a reparse point: ", "所选文件夹或其路径组件包含重解析点：") is var reparseFolder && reparseFolder != text ? reparseFolder
            : Translate("Selected folder could not be validated: ", "无法验证所选文件夹：");
    }

    private void UpdateDeleteEnabled()
    {
        if (deleteButton is null) return;
        deleteButton.Enabled = lastScan is not null && grid.Rows.Cast<DataGridViewRow>().Any(r => Convert.ToBoolean(r.Cells[0].Value ?? false));
    }

    private void DeleteSelected()
    {
        if (lastScan is null) return;
        var selectedPaths = grid.Rows.Cast<DataGridViewRow>().Where(r => Convert.ToBoolean(r.Cells[0].Value ?? false)).Select(r => Convert.ToString(r.Cells[1].Value)!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selected = lastScan.Files.Where(f => selectedPaths.Contains(f.FullPath)).ToArray();
        if (selected.Length == 0) return;
        var bytes = selected.Sum(f => f.Bytes);
        var prompt = chinese
            ? $"确定永久删除勾选的 {selected.Length:N0} 个文件（{FormatSize(bytes)}）吗？此操作无法撤销。"
            : $"Permanently delete {selected.Length:N0} checked files ({FormatSize(bytes)})? This action cannot be undone.";
        if (MessageBox.Show(this, prompt, chinese ? "确认删除" : "Confirm deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        var result = TempScanner.Delete(selected, rootsList.Items.Cast<string>().ToArray());
        status.Text = chinese ? $"已删除 {result.Deleted:N0}/{selected.Length:N0} 个勾选文件。" : $"Deleted {result.Deleted:N0} of {selected.Length:N0} checked files.";
        rawIssues = result.Issues.Take(300).ToArray();
        issues.Lines = rawIssues.Select(LocalizeIssue).ToArray();
        lastScan = null;
        deletionFinished = true;
        deleteButton.Enabled = false;
        summary.Text = chinese ? "删除后请重新扫描以查看当前内容。" : "Scan again to review current contents after deletion.";
        MessageBox.Show(this, chinese ? "删除操作已结束。请重新扫描以查看当前内容。" : "Deletion finished. Scan again to review current contents.", chinese ? "操作完成" : "Finished", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string FormatSize(long bytes) => bytes < 1024 ? $"{bytes} B" : bytes < 1024 * 1024 ? $"{bytes / 1024d:N1} KB" : bytes < 1024L * 1024 * 1024 ? $"{bytes / 1024d / 1024:N1} MB" : $"{bytes / 1024d / 1024 / 1024:N1} GB";
}

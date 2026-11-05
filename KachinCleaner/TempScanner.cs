using System.IO;

namespace KachinCleaner;

internal sealed record TempFile(string Group, string FullPath, long Bytes);
internal sealed record ScanResult(IReadOnlyList<TempFile> Files, IReadOnlyList<string> Issues);

internal static class TempScanner
{
    private static string[] AllowedRoots(List<string>? issues = null)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            issues?.Add("Local application data path is unavailable; no temp files were scanned.");
            return Array.Empty<string>();
        }

        var root = Path.GetFullPath(Path.Combine(localAppData, "Temp"));
        if (!string.Equals(Path.GetPathRoot(root), @"C:\", StringComparison.OrdinalIgnoreCase))
        {
            issues?.Add("Local application data Temp is not on C:; no temp files were scanned.");
            return Array.Empty<string>();
        }

        try
        {
            if (!Directory.Exists(root) || HasReparsePointInPath(root))
            {
                issues?.Add("Local application data Temp is missing or traverses a reparse point; no temp files were scanned.");
                return Array.Empty<string>();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            issues?.Add("Local application data Temp could not be validated; no temp files were scanned.");
            return Array.Empty<string>();
        }

        return new[] { root };
    }

    private static bool HasReparsePointInPath(string path)
    {
        var current = Path.GetFullPath(path);
        while (true)
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            var parent = Path.GetDirectoryName(current);
            if (parent is null || parent == current) break;
            current = parent;
        }
        return false;
    }

    public static Task<ScanResult> ScanAsync(IProgress<string> progress, CancellationToken token) => Task.Run(() => Scan(progress, token), token);

    private static ScanResult Scan(IProgress<string> progress, CancellationToken token)
    {
        var files = new List<TempFile>();
        var issues = new List<string>();
        foreach (var root in AllowedRoots(issues))
        {
            token.ThrowIfCancellationRequested();
            const string group = "LocalAppData Temp";
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var dir = pending.Pop();
                IEnumerable<string> entries;
                try { entries = Directory.EnumerateFileSystemEntries(dir).ToArray(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { issues.Add($"Skipped inaccessible directory: {dir}"); continue; }
                foreach (var path in entries)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var attrs = File.GetAttributes(path);
                        if ((attrs & FileAttributes.ReparsePoint) != 0) continue;
                        if ((attrs & FileAttributes.Directory) != 0) { pending.Push(path); continue; }
                        var info = new FileInfo(path);
                        if (!IsAllowed(path, root)) continue;
                        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) { }
                        files.Add(new TempFile(group, Path.GetFullPath(path), info.Length));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                    {
                        issues.Add($"Skipped inaccessible or in-use item: {path}");
                    }
                }
                progress.Report($"Scanning {group}: {files.Count:N0} files found");
            }
        }
        return new ScanResult(files, issues);
    }

    private static bool IsAllowed(string path, string root)
    {
        var full = Path.GetFullPath(path);
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static (int Deleted, List<string> Issues) Delete(IEnumerable<TempFile> candidates)
    {
        int deleted = 0;
        var issues = new List<string>();
        var roots = AllowedRoots(issues);
        foreach (var item in candidates)
        {
            try
            {
                var full = Path.GetFullPath(item.FullPath);
                var root = roots.FirstOrDefault(r => IsAllowed(full, r));
                if (root is null) { issues.Add($"Refused path outside approved roots: {full}"); continue; }
                var attrs = File.GetAttributes(full);
                if ((attrs & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) { issues.Add($"Refused changed item: {full}"); continue; }
                File.Delete(full);
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or FileNotFoundException)
            {
                issues.Add($"Could not delete: {item.FullPath}");
            }
        }
        return (deleted, issues);
    }
}

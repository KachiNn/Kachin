using System.IO;

namespace KachinCleaner;

internal sealed record TempFile(string Group, string FullPath, long Bytes);
internal sealed record ScanResult(IReadOnlyList<TempFile> Files, IReadOnlyList<string> Issues);

internal static class TempScanner
{
    private static readonly StringComparison PathComparison = StringComparison.OrdinalIgnoreCase;

    public static string? GetDefaultTempRoot(out string issue)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            issue = "Local application data path is unavailable.";
            return null;
        }

        try
        {
            var root = Path.GetFullPath(Path.Combine(localAppData, "Temp"));
            if (!string.Equals(Path.GetPathRoot(root), @"C:\", PathComparison))
            {
                issue = "Local application data Temp is not on C:, so it was not approved.";
                return null;
            }
            if (!Directory.Exists(root))
            {
                issue = $"Local application data Temp does not exist: {root}";
                return null;
            }
            if (HasReparsePointInPath(root))
            {
                issue = $"Local application data Temp contains a reparse-point path component: {root}";
                return null;
            }
            issue = string.Empty;
            return Path.TrimEndingDirectorySeparator(root);
        }
        catch (Exception ex) when (IsAccessOrPathError(ex))
        {
            issue = $"Local application data Temp could not be validated: {ex.Message}";
            return null;
        }
    }

    public static bool ValidateSelectableRoot(string path, out string issue)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                issue = "Select a folder path.";
                return false;
            }

            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            var volumeRoot = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full) ?? string.Empty);
            if (string.IsNullOrEmpty(full) || string.Equals(full, volumeRoot, PathComparison))
            {
                issue = "Drive roots cannot be selected. Choose an ordinary folder.";
                return false;
            }
            if (!Directory.Exists(full))
            {
                issue = $"Selected folder does not exist or is not a directory: {full}";
                return false;
            }
            if (IsProtectedPath(full))
            {
                issue = $"Protected Windows, Program Files, or user-profile path cannot be selected: {full}";
                return false;
            }
            if (HasReparsePointInPath(full))
            {
                issue = $"Selected folder or one of its path components is a reparse point: {full}";
                return false;
            }

            issue = string.Empty;
            return true;
        }
        catch (Exception ex) when (IsAccessOrPathError(ex))
        {
            issue = $"Selected folder could not be validated: {ex.Message}";
            return false;
        }
    }

    private static bool IsProtectedPath(string path)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var programFiles = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };

        if (!string.IsNullOrWhiteSpace(windows) && IsSameOrChild(full, windows)) return true;
        if (!string.IsNullOrWhiteSpace(profile) && string.Equals(full, Path.TrimEndingDirectorySeparator(Path.GetFullPath(profile)), PathComparison)) return true;
        return programFiles.Where(p => !string.IsNullOrWhiteSpace(p)).Any(p => IsSameOrChild(full, p));
    }

    private static bool IsSameOrChild(string path, string parent)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
        return string.Equals(full, root, PathComparison) || full.StartsWith(root + Path.DirectorySeparatorChar, PathComparison);
    }

    private static bool HasReparsePointInPath(string path)
    {
        var current = Path.GetFullPath(path);
        while (true)
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            var parent = Path.GetDirectoryName(current);
            if (parent is null || string.Equals(parent, current, PathComparison)) break;
            current = parent;
        }
        return false;
    }

    private static bool IsAccessOrPathError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or NotSupportedException;

    private static bool IsContainedFile(string path, string root)
    {
        var full = Path.GetFullPath(path);
        var canonicalRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var prefix = canonicalRoot + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, PathComparison);
    }

    private static string[] NormalizeRoots(IEnumerable<string> roots, List<string> issues)
    {
        var accepted = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in roots ?? Array.Empty<string>())
        {
            if (!ValidateSelectableRoot(candidate, out var issue))
            {
                issues.Add(issue);
                continue;
            }
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
            if (!seen.Add(full)) continue;
            if (accepted.Any(root => IsSameOrChild(full, root))) continue;
            accepted.RemoveAll(root => IsSameOrChild(root, full));
            accepted.Add(full);
        }
        return accepted.ToArray();
    }

    public static Task<ScanResult> ScanAsync(IEnumerable<string> roots, IProgress<string> progress, CancellationToken token) =>
        Task.Run(() => Scan(roots, progress, token), token);

    // Compatibility for existing UI callers until they pass the selected roots explicitly.
    public static Task<ScanResult> ScanAsync(IProgress<string> progress, CancellationToken token)
    {
        var root = GetDefaultTempRoot(out var issue);
        return root is null
            ? Task.FromResult(new ScanResult(Array.Empty<TempFile>(), new[] { issue }))
            : ScanAsync(new[] { root }, progress, token);
    }

    private static ScanResult Scan(IEnumerable<string> roots, IProgress<string> progress, CancellationToken token)
    {
        var files = new List<TempFile>();
        var issues = new List<string>();
        var scannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in NormalizeRoots(roots, issues))
        {
            token.ThrowIfCancellationRequested();
            var group = Path.GetFileName(root);
            if (string.IsNullOrWhiteSpace(group)) group = root;
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var dir = pending.Pop();
                IEnumerable<string> entries;
                try
                {
                    if (HasReparsePointInPath(dir)) continue;
                    entries = Directory.EnumerateFileSystemEntries(dir).ToArray();
                }
                catch (Exception ex) when (IsAccessOrPathError(ex))
                {
                    issues.Add($"Skipped inaccessible directory '{dir}': {ex.Message}");
                    continue;
                }

                foreach (var path in entries)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var attrs = File.GetAttributes(path);
                        if ((attrs & FileAttributes.ReparsePoint) != 0) continue;
                        if ((attrs & FileAttributes.Directory) != 0)
                        {
                            pending.Push(path);
                            continue;
                        }
                        var full = Path.GetFullPath(path);
                        if (!IsContainedFile(full, root) || HasReparsePointInPath(full) || !scannedPaths.Add(full)) continue;
                        var info = new FileInfo(full);
                        using (new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.None)) { }
                        files.Add(new TempFile(group, full, info.Length));
                    }
                    catch (Exception ex) when (IsAccessOrPathError(ex))
                    {
                        issues.Add($"Skipped inaccessible or in-use item '{path}': {ex.Message}");
                    }
                }
                progress?.Report($"Scanning {group}: {files.Count:N0} files found");
            }
        }
        return new ScanResult(files, issues);
    }

    public static (int Deleted, List<string> Issues) Delete(IEnumerable<TempFile> candidates, IEnumerable<string> approvedRoots)
    {
        var deleted = 0;
        var issues = new List<string>();
        var roots = NormalizeRoots(approvedRoots, issues);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in candidates ?? Array.Empty<TempFile>())
        {
            try
            {
                var full = Path.GetFullPath(item.FullPath);
                if (!seen.Add(full)) continue;
                var root = roots.FirstOrDefault(r => IsContainedFile(full, r));
                if (root is null)
                {
                    issues.Add($"Refused candidate outside approved folders: {full}");
                    continue;
                }
                if (!ValidateSelectableRoot(root, out var rootIssue))
                {
                    issues.Add($"Refused candidate because its approved folder is no longer valid: {rootIssue}");
                    continue;
                }
                if (IsProtectedPath(full) || HasReparsePointInPath(full))
                {
                    issues.Add($"Refused protected or reparse-point candidate: {full}");
                    continue;
                }
                var attrs = File.GetAttributes(full);
                if ((attrs & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                {
                    issues.Add($"Refused changed item; only ordinary candidate files can be deleted: {full}");
                    continue;
                }
                File.Delete(full);
                deleted++;
            }
            catch (Exception ex) when (IsAccessOrPathError(ex) || ex is FileNotFoundException)
            {
                issues.Add($"Could not delete '{item.FullPath}': {ex.Message}");
            }
        }
        return (deleted, issues);
    }

    // Legacy deletion remains confined to the default Temp tree.
    public static (int Deleted, List<string> Issues) Delete(IEnumerable<TempFile> candidates)
    {
        var root = GetDefaultTempRoot(out var issue);
        if (root is null) return (0, new List<string> { issue });
        return Delete(candidates, new[] { root });
    }
}

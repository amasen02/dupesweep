using System.IO.Enumeration;

namespace DupeSweep;

/// <summary>Walks the requested roots, applying size/extension/exclude/symlink filtering as it goes.</summary>
public static class FileScanner
{
    public static IEnumerable<FileEntry> Enumerate(
        IReadOnlyList<string> roots,
        ScanOptions options,
        Action<string>? onWarning = null,
        Func<string, bool, (List<string> Files, List<string> Directories)>? listEntries = null,
        Func<string, string>? directoryResolver = null)
    {
        var seenPaths = new HashSet<string>(PathComparer);
        var visitedDirectories = new HashSet<string>(PathComparer);
        var entryEnumerator = listEntries ?? DefaultListEntries;
        var resolveDirectory = directoryResolver ?? DefaultResolveDirectory;

        foreach (string root in roots)
        {
            string fullRoot = Path.GetFullPath(root);
            if (!Directory.Exists(fullRoot))
                throw new DirectoryNotFoundException($"directory not found: {root}");

            if (options.FollowSymlinks)
            {
                visitedDirectories.Add(resolveDirectory(fullRoot));
            }

            foreach (FileEntry entry in EnumerateDirectory(fullRoot, options, onWarning, entryEnumerator, resolveDirectory, visitedDirectories))
            {
                if (seenPaths.Add(entry.FullPath))
                    yield return entry;
            }
        }
    }

    private static IEnumerable<FileEntry> EnumerateDirectory(
        string directory,
        ScanOptions options,
        Action<string>? onWarning,
        Func<string, bool, (List<string> Files, List<string> Directories)> listEntries,
        Func<string, string> resolveDirectory,
        HashSet<string> visitedDirectories)
    {
        (List<string> files, List<string> subdirectories) = ListEntries(directory, options.Recursive, onWarning, listEntries);

        foreach (string file in files)
        {
            FileEntry? entry = TryCreateEntry(file, options);
            if (entry is not null)
                yield return entry;
        }

        foreach (string subdirectory in subdirectories)
        {
            if (!options.FollowSymlinks && IsReparsePoint(subdirectory))
                continue;
            if (IsExcluded(subdirectory, options.Excludes))
                continue;

            if (options.FollowSymlinks)
            {
                string canonicalSubdir = resolveDirectory(subdirectory);
                if (!visitedDirectories.Add(canonicalSubdir))
                    continue;
            }

            foreach (FileEntry entry in EnumerateDirectory(subdirectory, options, onWarning, listEntries, resolveDirectory, visitedDirectories))
                yield return entry;
        }
    }

    private static (List<string> Files, List<string> Directories) ListEntries(
        string directory,
        bool recursive,
        Action<string>? onWarning,
        Func<string, bool, (List<string> Files, List<string> Directories)> listEntries)
    {
        try
        {
            return listEntries(directory, recursive);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            onWarning?.Invoke($"skipped unreadable directory: {directory} ({ex.Message})");
            return ([], []);
        }
    }

    private static (List<string> Files, List<string> Directories) DefaultListEntries(string directory, bool recursive)
    {
        var files = Directory.EnumerateFiles(directory).ToList();
        var directories = recursive ? Directory.EnumerateDirectories(directory).ToList() : [];
        return (files, directories);
    }

    private static string DefaultResolveDirectory(string path)
    {
        try
        {
            var info = new DirectoryInfo(path);
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                FileSystemInfo? target = info.ResolveLinkTarget(returnFinalTarget: true);
                if (target is not null)
                    return Path.GetFullPath(target.FullName);
            }
            return Path.GetFullPath(info.FullName);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return Path.GetFullPath(path);
        }
    }

    private static FileEntry? TryCreateEntry(string path, ScanOptions options)
    {
        FileInfo info;
        try
        {
            info = new FileInfo(path);
            if (!info.Exists) return null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return null;
        }

        if (!options.FollowSymlinks && info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            return null;
        if (info.Length < options.MinSizeBytes)
            return null;
        if (options.Extensions is { Length: > 0 } extensions &&
            !extensions.Contains(info.Extension, StringComparer.OrdinalIgnoreCase))
            return null;
        if (IsExcluded(path, options.Excludes))
            return null;

        return new FileEntry(info.FullName, info.Length, info.LastWriteTimeUtc);
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    private static bool IsExcluded(string path, IReadOnlyList<string> excludePatterns)
    {
        if (excludePatterns.Count == 0) return false;

        string name = Path.GetFileName(path);
        foreach (string pattern in excludePatterns)
        {
            if (FileSystemName.MatchesSimpleExpression(pattern, name))
                return true;
        }
        return false;
    }

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}

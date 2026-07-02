using System.IO.Enumeration;

namespace DupeSweep;

/// <summary>Walks the requested roots, applying size/extension/exclude/symlink filtering as it goes.</summary>
public static class FileScanner
{
    public static IEnumerable<FileEntry> Enumerate(IReadOnlyList<string> roots, ScanOptions options)
    {
        foreach (string root in roots)
        {
            string fullRoot = Path.GetFullPath(root);
            if (!Directory.Exists(fullRoot))
                throw new DirectoryNotFoundException($"directory not found: {root}");

            foreach (FileEntry entry in EnumerateDirectory(fullRoot, options))
                yield return entry;
        }
    }

    private static IEnumerable<FileEntry> EnumerateDirectory(string directory, ScanOptions options)
    {
        (List<string> files, List<string> subdirectories) = ListEntries(directory, options.Recursive);

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

            foreach (FileEntry entry in EnumerateDirectory(subdirectory, options))
                yield return entry;
        }
    }

    private static (List<string> Files, List<string> Directories) ListEntries(string directory, bool recursive)
    {
        try
        {
            var files = Directory.EnumerateFiles(directory).ToList();
            var directories = recursive ? Directory.EnumerateDirectories(directory).ToList() : [];
            return (files, directories);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return ([], []);
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
}

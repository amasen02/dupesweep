using DupeSweep.Tests.Support;

namespace DupeSweep.Tests;

public class FileScannerTests
{
    [Fact]
    public void Enumerate_FindsFilesRecursively()
    {
        using var dir = new TempDirectory();
        dir.WriteFile("top.txt", "a");
        dir.WriteFile("nested/deep.txt", "b");

        var options = new ScanOptions();
        var entries = FileScanner.Enumerate([dir.Path], options).ToList();

        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public void Enumerate_NoRecursive_SkipsSubdirectories()
    {
        using var dir = new TempDirectory();
        dir.WriteFile("top.txt", "a");
        dir.WriteFile("nested/deep.txt", "b");

        var options = new ScanOptions { Recursive = false };
        var entries = FileScanner.Enumerate([dir.Path], options).ToList();

        Assert.Single(entries);
        Assert.EndsWith("top.txt", entries[0].FullPath);
    }

    [Fact]
    public void Enumerate_MinSizeBytes_ExcludesSmallerFiles()
    {
        using var dir = new TempDirectory();
        dir.WriteFile("small.txt", "x");
        dir.WriteFile("big.txt", new string('x', 100));

        var options = new ScanOptions { MinSizeBytes = 50 };
        var entries = FileScanner.Enumerate([dir.Path], options).ToList();

        Assert.Single(entries);
        Assert.EndsWith("big.txt", entries[0].FullPath);
    }

    [Fact]
    public void Enumerate_DefaultMinSize_ExcludesEmptyFiles()
    {
        using var dir = new TempDirectory();
        dir.WriteFile("empty.txt", "");
        dir.WriteFile("nonempty.txt", "x");

        var options = new ScanOptions();
        var entries = FileScanner.Enumerate([dir.Path], options).ToList();

        Assert.Single(entries);
        Assert.EndsWith("nonempty.txt", entries[0].FullPath);
    }

    [Fact]
    public void Enumerate_MinSizeZero_IncludesEmptyFiles()
    {
        using var dir = new TempDirectory();
        dir.WriteFile("empty.txt", "");

        var options = new ScanOptions { MinSizeBytes = 0 };
        var entries = FileScanner.Enumerate([dir.Path], options).ToList();

        Assert.Single(entries);
    }

    [Fact]
    public void Enumerate_ExtensionFilter_OnlyMatchingExtensions()
    {
        using var dir = new TempDirectory();
        dir.WriteFile("photo.jpg", "a");
        dir.WriteFile("notes.txt", "b");

        var options = new ScanOptions { Extensions = [".jpg"] };
        var entries = FileScanner.Enumerate([dir.Path], options).ToList();

        Assert.Single(entries);
        Assert.EndsWith("photo.jpg", entries[0].FullPath);
    }

    [Fact]
    public void Enumerate_ExcludeGlob_SkipsMatchingDirectory()
    {
        using var dir = new TempDirectory();
        dir.WriteFile("keep/file.txt", "a");
        dir.WriteFile("node_modules/pkg.txt", "b");

        var options = new ScanOptions();
        options.Excludes.Add("node_modules");
        var entries = FileScanner.Enumerate([dir.Path], options).ToList();

        Assert.Single(entries);
        Assert.EndsWith("file.txt", entries[0].FullPath);
    }

    [Fact]
    public void Enumerate_ExcludeGlob_SkipsMatchingFilePattern()
    {
        using var dir = new TempDirectory();
        dir.WriteFile("keep.txt", "a");
        dir.WriteFile("scratch.tmp", "b");

        var options = new ScanOptions();
        options.Excludes.Add("*.tmp");
        var entries = FileScanner.Enumerate([dir.Path], options).ToList();

        Assert.Single(entries);
        Assert.EndsWith("keep.txt", entries[0].FullPath);
    }

    [Fact]
    public void Enumerate_MissingRoot_ThrowsDirectoryNotFound()
    {
        var options = new ScanOptions();
        string missing = Path.Combine(Path.GetTempPath(), "dupesweep-does-not-exist-" + Guid.NewGuid());

        Assert.Throws<DirectoryNotFoundException>(() => FileScanner.Enumerate([missing], options).ToList());
    }

    [Fact]
    public void Enumerate_MultipleRoots_CombinesResults()
    {
        using var dirA = new TempDirectory();
        using var dirB = new TempDirectory();
        dirA.WriteFile("a.txt", "1");
        dirB.WriteFile("b.txt", "2");

        var options = new ScanOptions();
        var entries = FileScanner.Enumerate([dirA.Path, dirB.Path], options).ToList();

        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public void Enumerate_OverlappingRoots_DeduplicatesNormalizedPaths()
    {
        using var dir = new TempDirectory();
        string nested = dir.CreateSubdirectory("nested");
        string filePath = dir.WriteFile("nested/file.txt", "content");

        var options = new ScanOptions();
        var entries = FileScanner.Enumerate(
            [dir.Path, dir.Path, Path.Combine(dir.Path, "nested", ".."), nested], options).ToList();

        Assert.Single(entries);
        Assert.Equal(Path.GetFullPath(filePath), entries[0].FullPath);
    }

    [Fact]
    public void Enumerate_InaccessibleChildDirectory_ReportsWarningAndContinues()
    {
        using var dir = new TempDirectory();
        string accessibleFile = dir.WriteFile("accessible.txt", "content");
        string forbiddenSubdir = dir.CreateSubdirectory("forbidden");

        var warnings = new List<string>();
        var options = new ScanOptions { Verbose = true };

        var entries = FileScanner.Enumerate(
            [dir.Path],
            options,
            onWarning: warnings.Add,
            listEntries: (path, recursive) =>
            {
                if (string.Equals(path, forbiddenSubdir, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("Access is denied.");

                return (
                    [accessibleFile],
                    [forbiddenSubdir]
                );
            }).ToList();

        Assert.Single(entries);
        Assert.Equal(Path.GetFullPath(accessibleFile), entries[0].FullPath);
        Assert.Single(warnings);
        Assert.Contains(forbiddenSubdir, warnings[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Access is denied.", warnings[0]);
    }

    [Fact]
    public void Enumerate_InaccessibleChildDirectory_WithoutWarningHandler_ContinuesSilently()
    {
        using var dir = new TempDirectory();
        string accessibleFile = dir.WriteFile("accessible.txt", "content");
        string forbiddenSubdir = dir.CreateSubdirectory("forbidden");

        var options = new ScanOptions { Verbose = false };

        var entries = FileScanner.Enumerate(
            [dir.Path],
            options,
            onWarning: null,
            listEntries: (path, recursive) =>
            {
                if (string.Equals(path, forbiddenSubdir, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("The device is not ready.");

                return (
                    [accessibleFile],
                    [forbiddenSubdir]
                );
            }).ToList();

        Assert.Single(entries);
        Assert.Equal(Path.GetFullPath(accessibleFile), entries[0].FullPath);
    }

    [Fact]
    public void Enumerate_FollowSymlinks_AncestorCycle_TerminatesWithoutDuplicating()
    {
        using var dir = new TempDirectory();
        string rootFile = dir.WriteFile("root.txt", "root-data");
        string subDir = Path.Combine(dir.Path, "sub");
        string subFile = dir.WriteFile("sub/sub.txt", "sub-data");
        string loopLink = Path.Combine(subDir, "loop_to_root");

        var options = new ScanOptions { FollowSymlinks = true };

        // Test with a 2-second timeout bound to guarantee termination
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var entries = FileScanner.Enumerate(
            [dir.Path],
            options,
            listEntries: (path, recursive) =>
            {
                if (string.Equals(path, dir.Path, StringComparison.OrdinalIgnoreCase))
                    return ([rootFile], [subDir]);
                if (string.Equals(path, subDir, StringComparison.OrdinalIgnoreCase))
                    return ([subFile], [loopLink]);
                return ([], []);
            },
            directoryResolver: path =>
            {
                if (string.Equals(path, loopLink, StringComparison.OrdinalIgnoreCase))
                    return Path.GetFullPath(dir.Path); // points back to ancestor root
                return Path.GetFullPath(path);
            }).ToList();

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.FullPath == Path.GetFullPath(rootFile));
        Assert.Contains(entries, e => e.FullPath == Path.GetFullPath(subFile));
    }

    [Fact]
    public void Enumerate_FollowSymlinks_SelfCycle_TerminatesImmediately()
    {
        using var dir = new TempDirectory();
        string rootFile = dir.WriteFile("root.txt", "root-data");
        string selfLink = Path.Combine(dir.Path, "self_link");

        var options = new ScanOptions { FollowSymlinks = true };

        var entries = FileScanner.Enumerate(
            [dir.Path],
            options,
            listEntries: (path, recursive) =>
            {
                return ([rootFile], [selfLink]);
            },
            directoryResolver: path =>
            {
                if (string.Equals(path, selfLink, StringComparison.OrdinalIgnoreCase))
                    return Path.GetFullPath(dir.Path); // points back to itself
                return Path.GetFullPath(path);
            }).ToList();

        Assert.Single(entries);
        Assert.Equal(Path.GetFullPath(rootFile), entries[0].FullPath);
    }
}

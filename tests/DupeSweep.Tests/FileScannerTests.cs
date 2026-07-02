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
}

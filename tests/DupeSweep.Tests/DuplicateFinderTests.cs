using DupeSweep.Tests.Support;

namespace DupeSweep.Tests;

public class DuplicateFinderTests
{
    private static ScanOptions DefaultOptions() => new() { Parallelism = 4 };

    [Fact]
    public async Task FindGroupsAsync_IdenticalFiles_AreGrouped()
    {
        using var dir = new TempDirectory();
        string a = dir.WriteFile("a.txt", "same content");
        string b = dir.WriteFile("b.txt", "same content");
        string c = dir.WriteFile("c.txt", "same content");

        var entries = FileScanner.Enumerate([dir.Path], DefaultOptions()).ToList();
        var finder = new DuplicateFinder(DefaultOptions());

        DuplicateScanResult result = await finder.FindGroupsAsync(entries, CancellationToken.None);

        Assert.Single(result.Groups);
        Assert.Equal(3, result.Groups[0].Files.Count);
        Assert.Equal([a, b, c], result.Groups[0].Files.Select(f => f.FullPath).OrderBy(p => p));
    }

    [Fact]
    public async Task FindGroupsAsync_DifferentContent_NotGrouped()
    {
        using var dir = new TempDirectory();
        dir.WriteFile("a.txt", "content one");
        dir.WriteFile("b.txt", "content two");

        var entries = FileScanner.Enumerate([dir.Path], DefaultOptions()).ToList();
        var finder = new DuplicateFinder(DefaultOptions());

        DuplicateScanResult result = await finder.FindGroupsAsync(entries, CancellationToken.None);

        Assert.Empty(result.Groups);
    }

    [Fact]
    public async Task FindGroupsAsync_SameSizeDifferentContent_NotFalselyGrouped()
    {
        using var dir = new TempDirectory();
        // Same length, different bytes - must not collide just because sizes match.
        dir.WriteFile("a.txt", "aaaaaaaaaa");
        dir.WriteFile("b.txt", "bbbbbbbbbb");

        var entries = FileScanner.Enumerate([dir.Path], DefaultOptions()).ToList();
        var finder = new DuplicateFinder(DefaultOptions());

        DuplicateScanResult result = await finder.FindGroupsAsync(entries, CancellationToken.None);

        Assert.Empty(result.Groups);
    }

    [Fact]
    public async Task FindGroupsAsync_UniqueFile_IsNotAGroup()
    {
        using var dir = new TempDirectory();
        dir.WriteFile("only.txt", "nothing else matches this");

        var entries = FileScanner.Enumerate([dir.Path], DefaultOptions()).ToList();
        var finder = new DuplicateFinder(DefaultOptions());

        DuplicateScanResult result = await finder.FindGroupsAsync(entries, CancellationToken.None);

        Assert.Empty(result.Groups);
    }

    [Fact]
    public async Task FindGroupsAsync_MultipleGroups_OrderedByReclaimableBytesDescending()
    {
        using var dir = new TempDirectory();
        // Small group: 2 copies of a 5-byte file -> reclaimable 5 bytes.
        dir.WriteFile("small-a.txt", "aaaaa");
        dir.WriteFile("small-b.txt", "aaaaa");
        // Large group: 3 copies of a 20-byte file -> reclaimable 40 bytes.
        dir.WriteFile("large-a.txt", new string('b', 20));
        dir.WriteFile("large-b.txt", new string('b', 20));
        dir.WriteFile("large-c.txt", new string('b', 20));

        var entries = FileScanner.Enumerate([dir.Path], DefaultOptions()).ToList();
        var finder = new DuplicateFinder(DefaultOptions());

        DuplicateScanResult result = await finder.FindGroupsAsync(entries, CancellationToken.None);

        Assert.Equal(2, result.Groups.Count);
        Assert.True(result.Groups[0].ReclaimableBytes > result.Groups[1].ReclaimableBytes);
    }

    [Fact]
    public void ReclaimableBytes_ComputesSizeTimesExtraCopies()
    {
        var group = new DuplicateGroup("hash", 100, [
            new FileEntry("a", 100, DateTime.UtcNow),
            new FileEntry("b", 100, DateTime.UtcNow),
            new FileEntry("c", 100, DateTime.UtcNow),
        ]);

        Assert.Equal(200, group.ReclaimableBytes);
    }
}

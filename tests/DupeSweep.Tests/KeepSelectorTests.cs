namespace DupeSweep.Tests;

public class KeepSelectorTests
{
    private static readonly DateTime BaseTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyList<FileEntry> ThreeFiles() =>
    [
        new FileEntry(@"C:\dir\first.txt", 10, BaseTime),
        new FileEntry(@"C:\dir\second.txt", 10, BaseTime.AddDays(1)),
        new FileEntry(@"C:\a-very-long-directory-name\third.txt", 10, BaseTime.AddDays(-1)),
    ];

    [Fact]
    public void SelectKeeper_First_ReturnsFirstInScanOrder()
    {
        FileEntry keeper = KeepSelector.SelectKeeper(ThreeFiles(), KeepStrategy.First);

        Assert.Equal(@"C:\dir\first.txt", keeper.FullPath);
    }

    [Fact]
    public void SelectKeeper_Oldest_ReturnsEarliestLastWriteTime()
    {
        FileEntry keeper = KeepSelector.SelectKeeper(ThreeFiles(), KeepStrategy.Oldest);

        Assert.Equal(@"C:\a-very-long-directory-name\third.txt", keeper.FullPath);
    }

    [Fact]
    public void SelectKeeper_Newest_ReturnsLatestLastWriteTime()
    {
        FileEntry keeper = KeepSelector.SelectKeeper(ThreeFiles(), KeepStrategy.Newest);

        Assert.Equal(@"C:\dir\second.txt", keeper.FullPath);
    }

    [Fact]
    public void SelectKeeper_ShortestPath_ReturnsShortestFullPath()
    {
        FileEntry keeper = KeepSelector.SelectKeeper(ThreeFiles(), KeepStrategy.ShortestPath);

        Assert.Equal(@"C:\dir\first.txt", keeper.FullPath);
    }

    [Fact]
    public void Resolve_SeparatesKeepFromDuplicates()
    {
        var files = ThreeFiles();
        var group = new DuplicateGroup("hash", 10, files);

        DuplicateResolution resolution = KeepSelector.Resolve(group, KeepStrategy.First);

        Assert.Equal(files[0].FullPath, resolution.Keep.FullPath);
        Assert.Equal(2, resolution.Duplicates.Count);
        Assert.DoesNotContain(resolution.Duplicates, d => d.FullPath == resolution.Keep.FullPath);
    }
}

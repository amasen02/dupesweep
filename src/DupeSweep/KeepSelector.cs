namespace DupeSweep;

/// <summary>Chooses which file in a duplicate group survives, per the requested strategy.</summary>
public static class KeepSelector
{
    public static FileEntry SelectKeeper(IReadOnlyList<FileEntry> files, KeepStrategy strategy) => strategy switch
    {
        KeepStrategy.First => files[0],
        KeepStrategy.Oldest => files.OrderBy(f => f.LastWriteTimeUtc).ThenBy(f => f.FullPath, StringComparer.Ordinal).First(),
        KeepStrategy.Newest => files.OrderByDescending(f => f.LastWriteTimeUtc).ThenBy(f => f.FullPath, StringComparer.Ordinal).First(),
        KeepStrategy.ShortestPath => files.OrderBy(f => f.FullPath.Length).ThenBy(f => f.FullPath, StringComparer.Ordinal).First(),
        _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "unknown keep strategy"),
    };

    public static DuplicateResolution Resolve(DuplicateGroup group, KeepStrategy strategy)
    {
        FileEntry keeper = SelectKeeper(group.Files, strategy);
        List<FileEntry> duplicates = group.Files.Where(f => f.FullPath != keeper.FullPath).ToList();
        return new DuplicateResolution(group, keeper, duplicates);
    }
}

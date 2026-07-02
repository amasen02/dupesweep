using System.Collections.Concurrent;

namespace DupeSweep;

/// <summary>
/// Finds true duplicates through a three-stage funnel — cheap checks first, expensive ones last:
/// group by file size, then by a 64 KB quick hash, then confirm with a full SHA-256 hash.
/// </summary>
public sealed class DuplicateFinder(ScanOptions options)
{
    public async Task<DuplicateScanResult> FindGroupsAsync(IReadOnlyList<FileEntry> entries, CancellationToken ct)
    {
        var warnings = new List<string>();

        List<FileEntry> bySize = entries
            .GroupBy(e => e.Length)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .ToList();

        (Dictionary<string, List<FileEntry>> byQuickHash, List<string> quickWarnings) = await GroupByAsync(
            bySize,
            e => Task.FromResult(Hashing.ComputeQuickHash(e.FullPath, e.Length, ct)),
            ct).ConfigureAwait(false);
        warnings.AddRange(quickWarnings);

        List<FileEntry> candidates = byQuickHash.Values
            .Where(g => g.Count > 1)
            .SelectMany(g => g)
            .ToList();

        (Dictionary<string, List<FileEntry>> byFullHash, List<string> fullWarnings) = await GroupByAsync(
            candidates,
            e => Hashing.ComputeFullHashAsync(e.FullPath, ct),
            ct).ConfigureAwait(false);
        warnings.AddRange(fullWarnings);

        List<DuplicateGroup> groups = byFullHash
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => new DuplicateGroup(pair.Key, pair.Value[0].Length, pair.Value))
            .OrderByDescending(g => g.ReclaimableBytes)
            .ThenBy(g => g.Hash, StringComparer.Ordinal)
            .ToList();

        return new DuplicateScanResult(groups, warnings);
    }

    private async Task<(Dictionary<string, List<FileEntry>> Groups, List<string> Warnings)> GroupByAsync(
        IReadOnlyList<FileEntry> entries, Func<FileEntry, Task<string>> keySelector, CancellationToken ct)
    {
        var keys = new string?[entries.Count];
        var warnings = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, entries.Count),
            new ParallelOptions { MaxDegreeOfParallelism = options.Parallelism, CancellationToken = ct },
            async (i, token) =>
            {
                try
                {
                    keys[i] = await keySelector(entries[i]).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    warnings.Add($"skipped unreadable file: {entries[i].FullPath} ({ex.Message})");
                }
            }).ConfigureAwait(false);

        var groups = new Dictionary<string, List<FileEntry>>();
        for (int i = 0; i < entries.Count; i++)
        {
            if (keys[i] is not { } key) continue;
            if (!groups.TryGetValue(key, out List<FileEntry>? bucket))
                groups[key] = bucket = [];
            bucket.Add(entries[i]);
        }
        return (groups, [.. warnings]);
    }
}

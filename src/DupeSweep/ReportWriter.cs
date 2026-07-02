using System.Text.Json;

namespace DupeSweep;

/// <summary>Renders duplicate-group resolutions as either human-readable text or JSON.</summary>
public static class ReportWriter
{
    public static void WriteText(TextWriter writer, IReadOnlyList<DuplicateResolution> resolutions)
    {
        if (resolutions.Count == 0)
        {
            writer.WriteLine("dsweep: no duplicates found.");
            return;
        }

        long totalReclaimable = 0;
        int totalDuplicateFiles = 0;

        for (int i = 0; i < resolutions.Count; i++)
        {
            DuplicateResolution resolution = resolutions[i];
            totalReclaimable += resolution.Group.ReclaimableBytes;
            totalDuplicateFiles += resolution.Duplicates.Count;

            writer.WriteLine(
                $"[{i + 1}] {Format.Bytes(resolution.Group.FileSize)} x {resolution.Group.Files.Count} copies  (sha256 {resolution.Group.Hash[..12]}…)");
            writer.WriteLine($"    KEEP      {resolution.Keep.FullPath}");
            foreach (FileEntry duplicate in resolution.Duplicates)
                writer.WriteLine($"    DUPLICATE {duplicate.FullPath}");
        }

        writer.WriteLine();
        writer.WriteLine(
            $"dsweep: {resolutions.Count} duplicate group(s), {totalDuplicateFiles} reclaimable file(s), {Format.Bytes(totalReclaimable)} reclaimable.");
    }

    public static string WriteJson(IReadOnlyList<DuplicateResolution> resolutions)
    {
        var payload = new
        {
            groups = resolutions.Select(r => new
            {
                hash = r.Group.Hash,
                fileSize = r.Group.FileSize,
                reclaimableBytes = r.Group.ReclaimableBytes,
                keep = r.Keep.FullPath,
                duplicates = r.Duplicates.Select(d => d.FullPath).ToArray(),
            }),
            summary = new
            {
                groupCount = resolutions.Count,
                duplicateFileCount = resolutions.Sum(r => r.Duplicates.Count),
                reclaimableBytes = resolutions.Sum(r => r.Group.ReclaimableBytes),
            },
        };
        return JsonSerializer.Serialize(payload, DupeSweepJson.Options);
    }
}

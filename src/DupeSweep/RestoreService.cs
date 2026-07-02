using System.Text.Json;

namespace DupeSweep;

/// <summary>Reads a quarantine manifest and moves files back to their original locations.</summary>
public static class RestoreService
{
    public static IReadOnlyList<ManifestEntry> ReadManifest(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"manifest not found: {manifestPath}");

        string json = File.ReadAllText(manifestPath);
        return JsonSerializer.Deserialize<List<ManifestEntry>>(json, DupeSweepJson.Options)
               ?? throw new InvalidDataException($"manifest is empty or invalid: {manifestPath}");
    }

    public static RestoreSummary Restore(IReadOnlyList<ManifestEntry> manifest, bool dryRun)
    {
        int restored = 0;
        int skipped = 0;
        var messages = new List<string>();

        foreach (ManifestEntry entry in manifest)
        {
            if (!File.Exists(entry.QuarantinePath))
            {
                messages.Add($"skip: quarantined file missing: {entry.QuarantinePath}");
                skipped++;
                continue;
            }
            if (File.Exists(entry.OriginalPath))
            {
                messages.Add($"skip: destination already exists: {entry.OriginalPath}");
                skipped++;
                continue;
            }

            if (dryRun)
            {
                messages.Add($"would restore: {entry.QuarantinePath} -> {entry.OriginalPath}");
                restored++;
                continue;
            }

            string? directory = Path.GetDirectoryName(entry.OriginalPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.Move(entry.QuarantinePath, entry.OriginalPath);
            messages.Add($"restored: {entry.OriginalPath}");
            restored++;
        }

        return new RestoreSummary(restored, skipped, messages);
    }
}

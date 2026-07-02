using System.Globalization;
using System.Text.Json;

namespace DupeSweep;

/// <summary>
/// Moves duplicates into a quarantine folder instead of deleting them, and writes a manifest
/// so the move can be undone later with <c>dsweep restore</c>.
/// </summary>
public static class QuarantineService
{
    public static IReadOnlyList<ManifestEntry> Quarantine(IReadOnlyList<DuplicateResolution> resolutions, string quarantineDir)
    {
        Directory.CreateDirectory(quarantineDir);
        var manifest = new List<ManifestEntry>();

        for (int groupIndex = 0; groupIndex < resolutions.Count; groupIndex++)
        {
            DuplicateResolution resolution = resolutions[groupIndex];
            if (resolution.Duplicates.Count == 0) continue;

            string groupDir = Path.Combine(quarantineDir, groupIndex.ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(groupDir);

            foreach (FileEntry duplicate in resolution.Duplicates)
            {
                string destination = UniqueDestination(groupDir, Path.GetFileName(duplicate.FullPath));
                File.Move(duplicate.FullPath, destination);
                manifest.Add(new ManifestEntry(duplicate.FullPath, destination, duplicate.Length, resolution.Group.Hash));
            }
        }
        return manifest;
    }

    public static void WriteManifest(IReadOnlyList<ManifestEntry> manifest, string manifestPath)
    {
        string json = JsonSerializer.Serialize(manifest, DupeSweepJson.Options);
        File.WriteAllText(manifestPath, json);
    }

    private static string UniqueDestination(string directory, string fileName)
    {
        string destination = Path.Combine(directory, fileName);
        if (!File.Exists(destination)) return destination;

        string extension = Path.GetExtension(fileName);
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        for (int suffix = 2; ; suffix++)
        {
            string candidate = Path.Combine(directory, $"{baseName}_{suffix}{extension}");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}

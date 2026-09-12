using System.Globalization;
using System.Text.Json;

namespace DupeSweep;

/// <summary>
/// Plans and moves duplicates into a quarantine folder, writing a durable manifest
/// before the first move so a partial operation remains recoverable.
/// </summary>
public static class QuarantineService
{
    public static IReadOnlyList<ManifestEntry> Quarantine(IReadOnlyList<DuplicateResolution> resolutions, string quarantineDir)
    {
        string fullQuarantineDir = Path.GetFullPath(quarantineDir);
        string manifestPath = Path.Combine(fullQuarantineDir, "manifest.json");
        if (PathExists(manifestPath))
            throw new InvalidOperationException($"refusing to reuse existing quarantine manifest: {manifestPath}");

        Directory.CreateDirectory(fullQuarantineDir);
        var manifest = Plan(resolutions, fullQuarantineDir);

        // CreateNew closes the check/create race and ensures an existing manifest is
        // never replaced. Flush the complete journal before moving any source file.
        WriteManifest(manifest, manifestPath);

        foreach (ManifestEntry entry in manifest)
            File.Move(entry.OriginalPath, entry.QuarantinePath);

        return manifest;
    }

    public static void WriteManifest(IReadOnlyList<ManifestEntry> manifest, string manifestPath)
    {
        string? directory = Path.GetDirectoryName(manifestPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string json = JsonSerializer.Serialize(manifest, DupeSweepJson.Options);
        using var stream = new FileStream(manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream);
        writer.Write(json);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    private static List<ManifestEntry> Plan(IReadOnlyList<DuplicateResolution> resolutions, string quarantineDir)
    {
        var manifest = new List<ManifestEntry>();
        var reservedDestinations = new HashSet<string>(PathComparer);

        for (int groupIndex = 0; groupIndex < resolutions.Count; groupIndex++)
        {
            DuplicateResolution resolution = resolutions[groupIndex];
            if (resolution.Duplicates.Count == 0) continue;

            string groupDir = Path.Combine(quarantineDir, groupIndex.ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(groupDir);

            foreach (FileEntry duplicate in resolution.Duplicates)
            {
                string originalPath = Path.GetFullPath(duplicate.FullPath);
                string destination = UniqueDestination(groupDir, Path.GetFileName(originalPath), reservedDestinations);
                reservedDestinations.Add(destination);
                manifest.Add(new ManifestEntry(originalPath, destination, duplicate.Length, resolution.Group.Hash));
            }
        }

        return manifest;
    }

    private static string UniqueDestination(string directory, string fileName, ISet<string> reservedDestinations)
    {
        if (string.IsNullOrEmpty(fileName))
            throw new ArgumentException("duplicate path must have a file name", nameof(fileName));

        string destination = Path.Combine(directory, fileName);
        if (!PathExists(destination) && !reservedDestinations.Contains(destination)) return destination;

        string extension = Path.GetExtension(fileName);
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        for (int suffix = 2; ; suffix++)
        {
            string candidate = Path.Combine(directory, $"{baseName}_{suffix}{extension}");
            if (!PathExists(candidate) && !reservedDestinations.Contains(candidate)) return candidate;
        }
    }

    private static bool PathExists(string path) => File.Exists(path) || Directory.Exists(path);

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}

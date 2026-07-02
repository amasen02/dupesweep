using System.Diagnostics;
using System.Text;

namespace DupeSweep;

/// <summary>CLI entrypoint: parses options, scans, groups duplicates, reports, then optionally acts.</summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        ScanOptions options;
        try
        {
            options = CommandLine.Parse(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"dsweep: {ex.Message}");
            return 2;
        }

        if (options.Help)
        {
            Console.WriteLine(CommandLine.Usage);
            return 0;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.Error.WriteLine("\ndsweep: cancelling…");
            cancellation.Cancel();
        };

        try
        {
            return options.RestoreMode
                ? RunRestore(options)
                : await RunScanAsync(options, cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("dsweep: cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"dsweep: fatal: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunScanAsync(ScanOptions options, CancellationToken ct)
    {
        if (options.Roots.Count == 0)
        {
            Console.WriteLine(CommandLine.Usage);
            return 1;
        }

        var stopwatch = Stopwatch.StartNew();

        List<FileEntry> entries;
        try
        {
            entries = FileScanner.Enumerate(options.Roots, options).ToList();
        }
        catch (DirectoryNotFoundException ex)
        {
            Console.Error.WriteLine($"dsweep: {ex.Message}");
            return 1;
        }

        if (!options.Quiet)
            Console.Error.WriteLine($"dsweep: scanned {entries.Count} file(s), hashing candidates…");

        var finder = new DuplicateFinder(options);
        DuplicateScanResult scanResult = await finder.FindGroupsAsync(entries, ct).ConfigureAwait(false);

        if (options.Verbose)
            foreach (string warning in scanResult.Warnings)
                Console.Error.WriteLine($"dsweep: {warning}");

        List<DuplicateResolution> resolutions = scanResult.Groups
            .Select(g => KeepSelector.Resolve(g, options.Keep))
            .ToList();

        if (options.Json)
            Console.WriteLine(ReportWriter.WriteJson(resolutions));
        else
            ReportWriter.WriteText(Console.Out, resolutions);

        if (options.Apply != ApplyMode.None && resolutions.Count > 0)
            ApplyResolutions(options, resolutions);

        if (!options.Quiet)
            Console.Error.WriteLine($"dsweep: done in {Format.Duration(stopwatch.Elapsed)}.");

        return 0;
    }

    private static void ApplyResolutions(ScanOptions options, List<DuplicateResolution> resolutions)
    {
        if (options.Apply == ApplyMode.Quarantine)
        {
            string quarantineDir = options.QuarantineDir
                ?? Path.Combine(Path.GetFullPath(options.Roots[0]), ".dupesweep-quarantine");

            IReadOnlyList<ManifestEntry> manifest = QuarantineService.Quarantine(resolutions, quarantineDir);
            string manifestPath = Path.Combine(quarantineDir, "manifest.json");
            QuarantineService.WriteManifest(manifest, manifestPath);

            if (!options.Quiet)
                Console.Error.WriteLine(
                    $"dsweep: quarantined {manifest.Count} file(s) to {quarantineDir} (manifest: {manifestPath}).");
        }
        else if (options.Apply == ApplyMode.Delete)
        {
            int deleted = 0;
            foreach (DuplicateResolution resolution in resolutions)
            {
                foreach (FileEntry duplicate in resolution.Duplicates)
                {
                    File.Delete(duplicate.FullPath);
                    deleted++;
                }
            }

            if (!options.Quiet)
                Console.Error.WriteLine($"dsweep: permanently deleted {deleted} file(s).");
        }
    }

    private static int RunRestore(ScanOptions options)
    {
        if (string.IsNullOrEmpty(options.ManifestPath))
        {
            Console.WriteLine(CommandLine.Usage);
            return 1;
        }

        IReadOnlyList<ManifestEntry> manifest = RestoreService.ReadManifest(options.ManifestPath);
        RestoreSummary summary = RestoreService.Restore(manifest, options.DryRun);

        if (!options.Quiet)
            foreach (string message in summary.Messages)
                Console.WriteLine(message);

        Console.WriteLine(
            $"dsweep: restore {(options.DryRun ? "preview" : "complete")} — {summary.Restored} restored, {summary.Skipped} skipped.");

        return summary.Restored == 0 && summary.Skipped > 0 ? 1 : 0;
    }
}

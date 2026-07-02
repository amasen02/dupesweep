using System.Globalization;

namespace DupeSweep;

/// <summary>Parses argv into <see cref="ScanOptions"/> and renders usage.</summary>
public static class CommandLine
{
    public static ScanOptions Parse(string[] args)
    {
        var options = new ScanOptions();

        if (args.Length > 0 && string.Equals(args[0], "restore", StringComparison.OrdinalIgnoreCase))
        {
            options.RestoreMode = true;
            ParseRestoreArgs(args, options);
            return options;
        }

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "-h":
                case "--help":
                    options.Help = true;
                    break;
                case "--no-recursive":
                    options.Recursive = false;
                    break;
                case "--min-size":
                    options.MinSizeBytes = ParseSize(RequireValue(args, ref i, arg));
                    break;
                case "--ext":
                    options.Extensions = NormalizeExtensions(RequireValue(args, ref i, arg));
                    break;
                case "--exclude":
                    options.Excludes.Add(RequireValue(args, ref i, arg));
                    break;
                case "--follow-symlinks":
                    options.FollowSymlinks = true;
                    break;
                case "--json":
                    options.Json = true;
                    break;
                case "--apply":
                    options.Apply = ParseApplyMode(RequireValue(args, ref i, arg));
                    break;
                case "--quarantine-dir":
                    options.QuarantineDir = RequireValue(args, ref i, arg);
                    break;
                case "--keep":
                    options.Keep = ParseKeepStrategy(RequireValue(args, ref i, arg));
                    break;
                case "-j":
                case "--parallel":
                    options.Parallelism = Math.Clamp(int.Parse(RequireValue(args, ref i, arg), CultureInfo.InvariantCulture), 1, 64);
                    break;
                case "-q":
                case "--quiet":
                    options.Quiet = true;
                    break;
                case "-v":
                case "--verbose":
                    options.Verbose = true;
                    break;
                default:
                    if (arg.StartsWith('-'))
                        throw new ArgumentException($"unknown option '{arg}' (try --help)");
                    options.Roots.Add(arg);
                    break;
            }
        }

        return options;
    }

    private static void ParseRestoreArgs(string[] args, ScanOptions options)
    {
        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "-h":
                case "--help":
                    options.Help = true;
                    break;
                case "--dry-run":
                    options.DryRun = true;
                    break;
                case "-q":
                case "--quiet":
                    options.Quiet = true;
                    break;
                default:
                    if (arg.StartsWith('-'))
                        throw new ArgumentException($"unknown option '{arg}' (try --help)");
                    if (options.ManifestPath is not null)
                        throw new ArgumentException("restore takes exactly one manifest path");
                    options.ManifestPath = arg;
                    break;
            }
        }
    }

    private static string RequireValue(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException($"option '{flag}' requires a value");
        return args[++i];
    }

    private static long ParseSize(string raw)
    {
        raw = raw.Trim();
        if (raw.Length == 0)
            throw new ArgumentException("size value cannot be empty");

        char suffix = char.ToUpperInvariant(raw[^1]);
        long multiplier = suffix switch
        {
            'K' => 1024L,
            'M' => 1024L * 1024,
            'G' => 1024L * 1024 * 1024,
            _ => 1L,
        };
        string numberPart = multiplier == 1 ? raw : raw[..^1];
        if (!double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || value < 0)
            throw new ArgumentException($"invalid size '{raw}'");
        return (long)(value * multiplier);
    }

    private static string[] NormalizeExtensions(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
           .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant())
           .ToArray();

    private static ApplyMode ParseApplyMode(string raw) => raw.ToLowerInvariant() switch
    {
        "quarantine" => ApplyMode.Quarantine,
        "delete" => ApplyMode.Delete,
        _ => throw new ArgumentException($"invalid --apply mode '{raw}' (expected 'quarantine' or 'delete')"),
    };

    private static KeepStrategy ParseKeepStrategy(string raw) => raw.ToLowerInvariant() switch
    {
        "first" => KeepStrategy.First,
        "oldest" => KeepStrategy.Oldest,
        "newest" => KeepStrategy.Newest,
        "shortest-path" => KeepStrategy.ShortestPath,
        _ => throw new ArgumentException($"invalid --keep strategy '{raw}' (expected first, oldest, newest or shortest-path)"),
    };

    public static string Usage =>
        """
        dsweep - DupeSweep: a fast duplicate-file finder and reclaimer

        USAGE:
          dsweep <dir> [dir2 ...] [options]
          dsweep restore <manifest.json> [--dry-run]

        OPTIONS:
              --no-recursive       Only scan the given directories, not subdirectories.
              --min-size <size>    Ignore files smaller than this (default 1; 0 includes empty files), e.g. 4K, 10M.
              --ext <list>         Only consider these extensions, e.g. .jpg,.png
              --exclude <glob>     Exclude files/directories matching a glob (repeatable), e.g. node_modules, *.tmp
              --follow-symlinks    Follow symlinked files and directories (off by default; no cycle detection).
              --keep <strategy>    Which copy to keep per group: first (default), oldest, newest, shortest-path.
              --apply <mode>       Act on duplicates: 'quarantine' (movable/reversible) or 'delete' (permanent).
              --quarantine-dir <p> Destination for --apply quarantine (default: <first-dir>/.dupesweep-quarantine).
          -j, --parallel <n>       Hashing worker count (default: CPU core count, max 64).
              --json               Emit a machine-readable JSON report instead of text.
          -q, --quiet              Minimal output.
          -v, --verbose            Verbose logging (reports skipped/unreadable files).
          -h, --help               Show this help.

        RESTORE:
              --dry-run            Preview what would be restored without moving anything.

        EXAMPLES:
          dsweep ~/Downloads
          dsweep ~/Photos ~/Backups/Photos --ext .jpg,.png --keep oldest
          dsweep . --exclude node_modules --exclude .git --apply quarantine
          dsweep restore ~/Downloads/.dupesweep-quarantine/manifest.json
        """;
}

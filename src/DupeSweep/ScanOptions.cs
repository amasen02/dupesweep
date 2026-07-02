namespace DupeSweep;

public enum ApplyMode { None, Quarantine, Delete }

public enum KeepStrategy { First, Oldest, Newest, ShortestPath }

/// <summary>Parsed CLI configuration for both scan mode and restore mode.</summary>
public sealed class ScanOptions
{
    public bool Help { get; set; }
    public List<string> Roots { get; } = [];
    public bool Recursive { get; set; } = true;
    public long MinSizeBytes { get; set; } = 1;
    public string[]? Extensions { get; set; }
    public List<string> Excludes { get; } = [];
    public bool FollowSymlinks { get; set; }
    public bool Json { get; set; }
    public ApplyMode Apply { get; set; } = ApplyMode.None;
    public string? QuarantineDir { get; set; }
    public KeepStrategy Keep { get; set; } = KeepStrategy.First;
    public int Parallelism { get; set; } = Math.Clamp(Environment.ProcessorCount, 1, 64);
    public bool Quiet { get; set; }
    public bool Verbose { get; set; }

    public bool RestoreMode { get; set; }
    public string? ManifestPath { get; set; }
    public bool DryRun { get; set; }
}

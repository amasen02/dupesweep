namespace DupeSweep;

/// <summary>A scanned file and the metadata needed to group and select among duplicates.</summary>
public sealed record FileEntry(string FullPath, long Length, DateTime LastWriteTimeUtc);

/// <summary>A set of files confirmed byte-identical by a full SHA-256 hash match.</summary>
public sealed record DuplicateGroup(string Hash, long FileSize, IReadOnlyList<FileEntry> Files)
{
    public long ReclaimableBytes => FileSize * (Files.Count - 1);
}

/// <summary>A duplicate group with one file chosen to keep and the rest marked for action.</summary>
public sealed record DuplicateResolution(DuplicateGroup Group, FileEntry Keep, IReadOnlyList<FileEntry> Duplicates);

/// <summary>The groups found by a scan, plus any files that could not be read.</summary>
public sealed record DuplicateScanResult(IReadOnlyList<DuplicateGroup> Groups, IReadOnlyList<string> Warnings);

/// <summary>One quarantined file's original and quarantine location, enabling <c>dsweep restore</c>.</summary>
public sealed record ManifestEntry(string OriginalPath, string QuarantinePath, long Length, string Hash);

/// <summary>Outcome of a restore run.</summary>
public sealed record RestoreSummary(int Restored, int Skipped, IReadOnlyList<string> Messages);

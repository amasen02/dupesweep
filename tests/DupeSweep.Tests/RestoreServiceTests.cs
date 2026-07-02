using DupeSweep.Tests.Support;

namespace DupeSweep.Tests;

public class RestoreServiceTests
{
    [Fact]
    public void Restore_MovesFileBackToOriginalLocation()
    {
        using var dir = new TempDirectory();
        string quarantinePath = dir.WriteFile("quarantine/dup.txt", "content");
        string originalPath = Path.Combine(dir.Path, "original", "dup.txt");
        var manifest = new[] { new ManifestEntry(originalPath, quarantinePath, 7, "hash") };

        RestoreSummary summary = RestoreService.Restore(manifest, dryRun: false);

        Assert.Equal(1, summary.Restored);
        Assert.Equal(0, summary.Skipped);
        Assert.True(File.Exists(originalPath));
        Assert.False(File.Exists(quarantinePath));
    }

    [Fact]
    public void Restore_DryRun_DoesNotMoveAnything()
    {
        using var dir = new TempDirectory();
        string quarantinePath = dir.WriteFile("quarantine/dup.txt", "content");
        string originalPath = Path.Combine(dir.Path, "original", "dup.txt");
        var manifest = new[] { new ManifestEntry(originalPath, quarantinePath, 7, "hash") };

        RestoreSummary summary = RestoreService.Restore(manifest, dryRun: true);

        Assert.Equal(1, summary.Restored);
        Assert.True(File.Exists(quarantinePath), "dry run must not move the file");
        Assert.False(File.Exists(originalPath));
    }

    [Fact]
    public void Restore_MissingQuarantineFile_IsSkipped()
    {
        using var dir = new TempDirectory();
        string quarantinePath = Path.Combine(dir.Path, "quarantine", "gone.txt");
        string originalPath = Path.Combine(dir.Path, "original", "gone.txt");
        var manifest = new[] { new ManifestEntry(originalPath, quarantinePath, 7, "hash") };

        RestoreSummary summary = RestoreService.Restore(manifest, dryRun: false);

        Assert.Equal(0, summary.Restored);
        Assert.Equal(1, summary.Skipped);
    }

    [Fact]
    public void Restore_ExistingDestination_IsSkippedWithoutOverwrite()
    {
        using var dir = new TempDirectory();
        string quarantinePath = dir.WriteFile("quarantine/dup.txt", "quarantined content");
        string originalPath = dir.WriteFile("original/dup.txt", "someone recreated this file");

        var manifest = new[] { new ManifestEntry(originalPath, quarantinePath, 7, "hash") };

        RestoreSummary summary = RestoreService.Restore(manifest, dryRun: false);

        Assert.Equal(0, summary.Restored);
        Assert.Equal(1, summary.Skipped);
        Assert.Equal("someone recreated this file", File.ReadAllText(originalPath));
        Assert.True(File.Exists(quarantinePath), "the quarantined file must be preserved, not silently lost");
    }

    [Fact]
    public void ReadManifest_MissingFile_Throws()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), "dupesweep-missing-manifest-" + Guid.NewGuid() + ".json");

        Assert.Throws<FileNotFoundException>(() => RestoreService.ReadManifest(missingPath));
    }
}

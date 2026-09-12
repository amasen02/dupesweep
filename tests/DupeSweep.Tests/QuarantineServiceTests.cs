using DupeSweep.Tests.Support;

namespace DupeSweep.Tests;

public class QuarantineServiceTests
{
    [Fact]
    public void Quarantine_MovesDuplicatesAndKeepsSurvivor()
    {
        using var dir = new TempDirectory();
        string keepPath = dir.WriteFile("keep.txt", "dup content");
        string dupPath = dir.WriteFile("dup.txt", "dup content");
        string quarantineDir = dir.CreateSubdirectory(".dupesweep-quarantine");

        var keep = new FileEntry(keepPath, 11, DateTime.UtcNow);
        var duplicate = new FileEntry(dupPath, 11, DateTime.UtcNow);
        var group = new DuplicateGroup("hash", 11, [keep, duplicate]);
        var resolution = new DuplicateResolution(group, keep, [duplicate]);

        var manifest = QuarantineService.Quarantine([resolution], quarantineDir);

        Assert.True(File.Exists(keepPath), "the kept file must not be moved");
        Assert.False(File.Exists(dupPath), "the duplicate must be moved out of its original location");
        Assert.Single(manifest);
        Assert.Equal(dupPath, manifest[0].OriginalPath);
        Assert.True(File.Exists(manifest[0].QuarantinePath));
    }

    [Fact]
    public void Quarantine_NameCollisionWithinOperation_GetsUniqueSuffixAndDoesNotOverwrite()
    {
        using var dir = new TempDirectory();
        string keepA = dir.WriteFile("groupA/keep.txt", "content A");
        string dupA = dir.WriteFile("groupA/dup.txt", "content A");
        string quarantineDir = dir.CreateSubdirectory(".dupesweep-quarantine");

        var keep = new FileEntry(keepA, 9, DateTime.UtcNow);
        var duplicate = new FileEntry(dupA, 9, DateTime.UtcNow);
        var group = new DuplicateGroup("hash", 9, [keep, duplicate]);

        string dupB = dir.WriteFile("groupB/dup.txt", "content A");
        var duplicateB = new FileEntry(dupB, 9, DateTime.UtcNow);
        var resolution = new DuplicateResolution(group, keep, [duplicate, duplicateB]);
        string groupDir = Path.Combine(quarantineDir, "0");
        Directory.CreateDirectory(groupDir);
        string preexisting = dir.WriteFile("preexisting", "content A");
        File.Move(preexisting, Path.Combine(groupDir, "dup.txt"));
        Directory.CreateDirectory(Path.Combine(groupDir, "dup_2.txt"));

        var manifest = QuarantineService.Quarantine([resolution], quarantineDir);

        Assert.Equal(Path.Combine(groupDir, "dup_3.txt"), manifest[0].QuarantinePath);
        Assert.Equal(Path.Combine(groupDir, "dup_4.txt"), manifest[1].QuarantinePath);
        Assert.True(File.Exists(manifest[0].QuarantinePath));
        Assert.True(File.Exists(manifest[1].QuarantinePath));
        Assert.Equal("content A", File.ReadAllText(Path.Combine(groupDir, "dup.txt")));
    }

    [Fact]
    public void Quarantine_SkipsGroupsWithNoDuplicates()
    {
        using var dir = new TempDirectory();
        string onlyFile = dir.WriteFile("solo.txt", "solo");
        string quarantineDir = dir.CreateSubdirectory(".dupesweep-quarantine");

        var keep = new FileEntry(onlyFile, 4, DateTime.UtcNow);
        var group = new DuplicateGroup("hash", 4, [keep]);
        var resolution = new DuplicateResolution(group, keep, []);

        var manifest = QuarantineService.Quarantine([resolution], quarantineDir);

        Assert.Empty(manifest);
        Assert.True(File.Exists(onlyFile));
    }

    [Fact]
    public void WriteManifest_RoundTripsThroughRestoreService()
    {
        using var dir = new TempDirectory();
        string keepPath = dir.WriteFile("keep.txt", "dup content");
        string dupPath = dir.WriteFile("dup.txt", "dup content");
        string quarantineDir = dir.CreateSubdirectory(".dupesweep-quarantine");

        var keep = new FileEntry(keepPath, 11, DateTime.UtcNow);
        var duplicate = new FileEntry(dupPath, 11, DateTime.UtcNow);
        var group = new DuplicateGroup("hash", 11, [keep, duplicate]);
        var resolution = new DuplicateResolution(group, keep, [duplicate]);

        var manifest = QuarantineService.Quarantine([resolution], quarantineDir);
        string manifestPath = Path.Combine(dir.Path, "manual-manifest.json");
        QuarantineService.WriteManifest(manifest, manifestPath);

        var reloaded = RestoreService.ReadManifest(manifestPath);

        Assert.Single(reloaded);
        Assert.Equal(manifest[0].OriginalPath, reloaded[0].OriginalPath);
        Assert.Equal(manifest[0].QuarantinePath, reloaded[0].QuarantinePath);
    }

    [Fact]
    public void Quarantine_RefusesExistingManifestBeforeMovingAnything()
    {
        using var dir = new TempDirectory();
        string keepPath = dir.WriteFile("keep.txt", "dup content");
        string dupPath = dir.WriteFile("dup.txt", "dup content");
        string quarantineDir = dir.CreateSubdirectory(".dupesweep-quarantine");
        string manifestPath = Path.Combine(quarantineDir, "manifest.json");
        File.WriteAllText(manifestPath, "sentinel");

        var keep = new FileEntry(keepPath, 11, DateTime.UtcNow);
        var duplicate = new FileEntry(dupPath, 11, DateTime.UtcNow);
        var group = new DuplicateGroup("hash", 11, [keep, duplicate]);
        var resolution = new DuplicateResolution(group, keep, [duplicate]);

        Assert.Throws<InvalidOperationException>(() => QuarantineService.Quarantine([resolution], quarantineDir));
        Assert.True(File.Exists(dupPath));
        Assert.Equal("sentinel", File.ReadAllText(manifestPath));
    }

    [Fact]
    public void Quarantine_PartialMoveLeavesFullManifestForRestore()
    {
        using var dir = new TempDirectory();
        string keepPath = dir.WriteFile("keep.txt", "dup content");
        string firstDuplicatePath = dir.WriteFile("first.txt", "dup content");
        string missingDuplicatePath = Path.Combine(dir.Path, "missing.txt");
        string quarantineDir = dir.CreateSubdirectory(".dupesweep-quarantine");

        var keep = new FileEntry(keepPath, 11, DateTime.UtcNow);
        var firstDuplicate = new FileEntry(firstDuplicatePath, 11, DateTime.UtcNow);
        var missingDuplicate = new FileEntry(missingDuplicatePath, 11, DateTime.UtcNow);
        var group = new DuplicateGroup("hash", 11, [keep, firstDuplicate, missingDuplicate]);
        var resolution = new DuplicateResolution(group, keep, [firstDuplicate, missingDuplicate]);

        Assert.ThrowsAny<IOException>(() => QuarantineService.Quarantine([resolution], quarantineDir));

        string manifestPath = Path.Combine(quarantineDir, "manifest.json");
        var manifest = RestoreService.ReadManifest(manifestPath);
        Assert.Equal(2, manifest.Count);
        Assert.False(File.Exists(firstDuplicatePath));
        Assert.True(File.Exists(manifest[0].QuarantinePath));

        RestoreSummary summary = RestoreService.Restore(manifest, dryRun: false);
        Assert.Equal(1, summary.Restored);
        Assert.Equal(1, summary.Skipped);
        Assert.True(File.Exists(firstDuplicatePath));
        Assert.Equal("dup content", File.ReadAllText(firstDuplicatePath));
    }
}

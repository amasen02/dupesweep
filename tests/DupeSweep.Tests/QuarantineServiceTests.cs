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
    public void Quarantine_NameCollisionAcrossGroups_GetsUniqueSuffix()
    {
        using var dir = new TempDirectory();
        string keepA = dir.WriteFile("groupA/keep.txt", "content A");
        string dupA = dir.WriteFile("groupA/dup.txt", "content A");
        string quarantineDir = dir.CreateSubdirectory(".dupesweep-quarantine");

        var keep = new FileEntry(keepA, 9, DateTime.UtcNow);
        var duplicate = new FileEntry(dupA, 9, DateTime.UtcNow);
        var group = new DuplicateGroup("hash", 9, [keep, duplicate]);

        // Two resolutions whose duplicate files share the same file name ("dup.txt") but
        // live in the same quarantine group folder — the second must not overwrite the first.
        var resolution = new DuplicateResolution(group, keep, [duplicate]);
        string dupB = dir.WriteFile("groupB/dup.txt", "content B");
        var duplicateB = new FileEntry(dupB, 9, DateTime.UtcNow);
        var resolutionSameGroupIndex = new DuplicateResolution(group, keep, [duplicateB]);

        // Force both duplicates into the same quarantine group directory (index 0) to prove
        // the collision-avoidance logic works even for identically named files.
        var manifest = QuarantineService.Quarantine([resolution], quarantineDir);
        var manifest2 = QuarantineService.Quarantine([resolutionSameGroupIndex], quarantineDir);

        Assert.NotEqual(manifest[0].QuarantinePath, manifest2[0].QuarantinePath);
        Assert.True(File.Exists(manifest[0].QuarantinePath));
        Assert.True(File.Exists(manifest2[0].QuarantinePath));
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
        string manifestPath = Path.Combine(quarantineDir, "manifest.json");
        QuarantineService.WriteManifest(manifest, manifestPath);

        var reloaded = RestoreService.ReadManifest(manifestPath);

        Assert.Single(reloaded);
        Assert.Equal(manifest[0].OriginalPath, reloaded[0].OriginalPath);
        Assert.Equal(manifest[0].QuarantinePath, reloaded[0].QuarantinePath);
    }
}

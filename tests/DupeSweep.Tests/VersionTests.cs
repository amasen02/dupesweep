namespace DupeSweep.Tests;

public class VersionTests
{
    [Fact]
    public void BuildInfo_Version_IsNotBlank()
    {
        Assert.False(string.IsNullOrWhiteSpace(BuildInfo.Version));
    }

    [Fact]
    public void BuildInfo_Version_DropsSourceRevisionMetadata()
    {
        // The SDK can append "+<revision>"; the reported release version must stay stable.
        Assert.DoesNotContain('+', BuildInfo.Version);
        Assert.Equal(BuildInfo.Version.Trim(), BuildInfo.Version);
    }

    [Fact]
    public void BuildInfo_Describe_NamesProductAndVersion()
    {
        Assert.Equal($"{BuildInfo.ProductName} {BuildInfo.Version}", BuildInfo.Describe());
        Assert.StartsWith("DupeSweep ", BuildInfo.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_VersionAlone_ExitsSuccessfully()
    {
        int exitCode = await Program.Main(["--version"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Main_VersionWithDirectoryArgument_ExitsWithoutScanning()
    {
        // This path does not exist, so a real scan would fail with a non-zero exit code.
        // Returning 0 proves --version short-circuits before any filesystem walk.
        string missingDirectory = Path.Combine(Path.GetTempPath(), $"dsweep-version-{Guid.NewGuid():N}");

        int exitCode = await Program.Main(["--version", missingDirectory]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Main_VersionInRestoreMode_ExitsSuccessfully()
    {
        int exitCode = await Program.Main(["restore", "--version"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Main_WithoutVersion_StillReportsMissingRoots()
    {
        // Existing behaviour: no directory argument and no meta flag prints usage and fails.
        int exitCode = await Program.Main([]);

        Assert.Equal(1, exitCode);
    }
}

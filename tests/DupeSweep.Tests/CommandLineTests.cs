namespace DupeSweep.Tests;

public class CommandLineTests
{
    [Fact]
    public void Parse_Defaults_AreSensible()
    {
        ScanOptions options = CommandLine.Parse(["some-dir"]);

        Assert.Equal(["some-dir"], options.Roots);
        Assert.True(options.Recursive);
        Assert.Equal(1, options.MinSizeBytes);
        Assert.Equal(ApplyMode.None, options.Apply);
        Assert.Equal(KeepStrategy.First, options.Keep);
        Assert.False(options.Json);
        Assert.False(options.RestoreMode);
    }

    [Fact]
    public void Parse_MultipleRoots_AllCaptured()
    {
        ScanOptions options = CommandLine.Parse(["dirA", "dirB"]);

        Assert.Equal(["dirA", "dirB"], options.Roots);
    }

    [Fact]
    public void Parse_NoRecursive_SetsFlag()
    {
        ScanOptions options = CommandLine.Parse(["dir", "--no-recursive"]);

        Assert.False(options.Recursive);
    }

    [Theory]
    [InlineData("4K", 4L * 1024)]
    [InlineData("10M", 10L * 1024 * 1024)]
    [InlineData("1G", 1024L * 1024 * 1024)]
    [InlineData("512", 512L)]
    public void Parse_MinSize_ParsesSuffixes(string raw, long expectedBytes)
    {
        ScanOptions options = CommandLine.Parse(["dir", "--min-size", raw]);

        Assert.Equal(expectedBytes, options.MinSizeBytes);
    }

    [Fact]
    public void Parse_Extensions_NormalizesToLowercaseWithDot()
    {
        ScanOptions options = CommandLine.Parse(["dir", "--ext", "JPG,.PNG"]);

        Assert.NotNull(options.Extensions);
        Assert.Equal([".jpg", ".png"], options.Extensions);
    }

    [Fact]
    public void Parse_ExcludeRepeatable_CollectsAllPatterns()
    {
        ScanOptions options = CommandLine.Parse(["dir", "--exclude", "node_modules", "--exclude", "*.tmp"]);

        Assert.Equal(["node_modules", "*.tmp"], options.Excludes);
    }

    [Theory]
    [InlineData("quarantine", ApplyMode.Quarantine)]
    [InlineData("delete", ApplyMode.Delete)]
    [InlineData("QUARANTINE", ApplyMode.Quarantine)]
    public void Parse_Apply_ParsesKnownModes(string raw, ApplyMode expected)
    {
        ScanOptions options = CommandLine.Parse(["dir", "--apply", raw]);

        Assert.Equal(expected, options.Apply);
    }

    [Fact]
    public void Parse_Apply_RejectsUnknownMode()
    {
        Assert.Throws<ArgumentException>(() => CommandLine.Parse(["dir", "--apply", "shred"]));
    }

    [Theory]
    [InlineData("oldest", KeepStrategy.Oldest)]
    [InlineData("newest", KeepStrategy.Newest)]
    [InlineData("shortest-path", KeepStrategy.ShortestPath)]
    public void Parse_Keep_ParsesKnownStrategies(string raw, KeepStrategy expected)
    {
        ScanOptions options = CommandLine.Parse(["dir", "--keep", raw]);

        Assert.Equal(expected, options.Keep);
    }

    [Fact]
    public void Parse_UnknownFlag_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CommandLine.Parse(["--nope"]));
        Assert.Contains("--nope", ex.Message);
    }

    [Fact]
    public void Parse_FlagMissingValue_Throws()
    {
        Assert.Throws<ArgumentException>(() => CommandLine.Parse(["dir", "--min-size"]));
    }

    [Fact]
    public void Parse_ParallelClampsToMax64()
    {
        ScanOptions options = CommandLine.Parse(["dir", "--parallel", "9999"]);

        Assert.Equal(64, options.Parallelism);
    }

    [Fact]
    public void Parse_Help_SetsFlagRegardlessOfOtherArgs()
    {
        ScanOptions options = CommandLine.Parse(["--help"]);

        Assert.True(options.Help);
    }

    [Fact]
    public void Parse_Restore_SetsRestoreModeAndManifestPath()
    {
        ScanOptions options = CommandLine.Parse(["restore", "manifest.json"]);

        Assert.True(options.RestoreMode);
        Assert.Equal("manifest.json", options.ManifestPath);
        Assert.False(options.DryRun);
    }

    [Fact]
    public void Parse_RestoreDryRun_SetsFlag()
    {
        ScanOptions options = CommandLine.Parse(["restore", "manifest.json", "--dry-run"]);

        Assert.True(options.DryRun);
    }

    [Fact]
    public void Parse_RestoreTwoManifestPaths_Throws()
    {
        Assert.Throws<ArgumentException>(() => CommandLine.Parse(["restore", "a.json", "b.json"]));
    }
}

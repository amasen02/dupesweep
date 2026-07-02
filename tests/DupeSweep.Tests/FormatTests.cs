namespace DupeSweep.Tests;

public class FormatTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1024 * 1024, "1 MB")]
    [InlineData(1024L * 1024 * 1024, "1 GB")]
    public void Bytes_FormatsHumanReadable(long bytes, string expected)
    {
        Assert.Equal(expected, Format.Bytes(bytes));
    }

    [Fact]
    public void Bytes_RejectsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Format.Bytes(-1));
    }

    [Fact]
    public void Duration_UsesSecondsUnderOneMinute()
    {
        Assert.Equal("2.5s", Format.Duration(TimeSpan.FromSeconds(2.5)));
    }

    [Fact]
    public void Duration_UsesMinutesSecondsAtOrOverOneMinute()
    {
        Assert.Equal("01:05", Format.Duration(TimeSpan.FromSeconds(65)));
    }
}

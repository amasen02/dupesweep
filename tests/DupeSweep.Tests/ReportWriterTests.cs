using System.Text.Json;

namespace DupeSweep.Tests;

public class ReportWriterTests
{
    private static DuplicateResolution SampleResolution()
    {
        var keep = new FileEntry("/data/keep.txt", 100, DateTime.UtcNow);
        var duplicate = new FileEntry("/data/dup.txt", 100, DateTime.UtcNow);
        var group = new DuplicateGroup(new string('a', 64), 100, [keep, duplicate]);
        return new DuplicateResolution(group, keep, [duplicate]);
    }

    [Fact]
    public void WriteText_NoDuplicates_ReportsNoneFound()
    {
        var writer = new StringWriter();

        ReportWriter.WriteText(writer, []);

        Assert.Contains("no duplicates found", writer.ToString());
    }

    [Fact]
    public void WriteText_ListsKeepAndDuplicatePaths()
    {
        var writer = new StringWriter();

        ReportWriter.WriteText(writer, [SampleResolution()]);

        string output = writer.ToString();
        Assert.Contains("KEEP      /data/keep.txt", output);
        Assert.Contains("DUPLICATE /data/dup.txt", output);
        Assert.Contains("1 duplicate group(s)", output);
    }

    [Fact]
    public void WriteJson_ProducesParseableSummary()
    {
        string json = ReportWriter.WriteJson([SampleResolution()]);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.Equal(1, root.GetProperty("summary").GetProperty("groupCount").GetInt32());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("duplicateFileCount").GetInt32());
        Assert.Equal(100, root.GetProperty("summary").GetProperty("reclaimableBytes").GetInt64());

        JsonElement group = root.GetProperty("groups")[0];
        Assert.Equal("/data/keep.txt", group.GetProperty("keep").GetString());
        Assert.Equal("/data/dup.txt", group.GetProperty("duplicates")[0].GetString());
    }

    [Fact]
    public void WriteJson_NoDuplicates_EmptyGroupsArray()
    {
        string json = ReportWriter.WriteJson([]);

        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(0, document.RootElement.GetProperty("groups").GetArrayLength());
    }
}

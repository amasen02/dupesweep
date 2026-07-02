namespace DupeSweep.Tests.Support;

/// <summary>A scratch directory under the OS temp folder that deletes itself on dispose.</summary>
public sealed class TempDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateDirectory(
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dupesweep-tests-" + Guid.NewGuid().ToString("N"))).FullName;

    public string WriteFile(string relativePath, string content)
    {
        string fullPath = System.IO.Path.Combine(Path, relativePath);
        string? directory = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public string CreateSubdirectory(string relativePath)
    {
        return Directory.CreateDirectory(System.IO.Path.Combine(Path, relativePath)).FullName;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; the OS temp folder gets reaped eventually regardless.
        }
    }
}

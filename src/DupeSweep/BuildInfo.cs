using System.Reflection;

namespace DupeSweep;

/// <summary>
/// Exposes the release version compiled into this assembly so a bug report can name the exact build.
/// </summary>
/// <remarks>
/// The value is read from the assembly's informational version, which the .NET SDK derives from the
/// MSBuild <c>Version</c> property declared once in <c>DupeSweep.csproj</c>. Nothing here hardcodes a
/// version string, so a release that builds with a different <c>Version</c> reports that value too.
/// </remarks>
public static class BuildInfo
{
    /// <summary>Product name used in user-facing version output.</summary>
    public const string ProductName = "DupeSweep";

    /// <summary>
    /// The release version, for example <c>1.0.1</c>. Build metadata (the part after <c>+</c> that the
    /// SDK may append from source-control revision) is dropped so the reported value matches the
    /// published release label.
    /// </summary>
    public static string Version { get; } = ResolveVersion();

    /// <summary>The single line printed by <c>dsweep --version</c>.</summary>
    public static string Describe() => $"{ProductName} {Version}";

    private static string ResolveVersion()
    {
        Assembly assembly = typeof(BuildInfo).Assembly;

        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            int metadataSeparator = informationalVersion.IndexOf('+');
            return (metadataSeparator >= 0 ? informationalVersion[..metadataSeparator] : informationalVersion).Trim();
        }

        Version? assemblyVersion = assembly.GetName().Version;
        return assemblyVersion is null ? "unknown" : assemblyVersion.ToString(3);
    }
}

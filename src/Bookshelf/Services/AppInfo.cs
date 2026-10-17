using System.Reflection;

namespace Bookshelf.Services;

public static class AppInfo
{
    /// <summary>The project's &lt;Version&gt; (build number stamped by the pipeline), without the source hash.</summary>
    public static string Version { get; } = GetVersion();

    private static string GetVersion()
    {
        var informational = typeof(AppInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return informational?.Split('+')[0]
            ?? typeof(AppInfo).Assembly.GetName().Version?.ToString(3)
            ?? "0.0.0";
    }
}

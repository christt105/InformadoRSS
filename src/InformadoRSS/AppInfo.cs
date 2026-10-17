using System.Reflection;

namespace InformadoRSS;

internal static class AppInfo
{
    public const int DefaultPort = 8420;

    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";
}

using System.Reflection;

namespace Cleanuparr.Shared.Helpers;

/// <summary>
/// The User-Agent product token, shaped per RFC 9110.
/// </summary>
public static class AppUserAgent
{
    private const string ProductName = "Cleanuparr";

    /// <summary>
    /// The User-Agent value for the running build.
    /// Reads "Cleanuparr/1.2.3" on a tagged release.
    /// </summary>
    public static readonly string Value = Format(Assembly.GetEntryAssembly()?.GetName().Version);

    internal static string Format(Version? version) =>
        version is null
            ? ProductName
            : $"{ProductName}/{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
}

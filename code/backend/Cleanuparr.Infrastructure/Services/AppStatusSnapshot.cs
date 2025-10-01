using Cleanuparr.Infrastructure.Models;

namespace Cleanuparr.Infrastructure.Services;

public sealed class AppStatusSnapshot
{
    private readonly object _sync = new();
    private string? _currentVersion;
    private string? _latestVersion;

    public AppStatus Current
    {
        get
        {
            lock (_sync)
            {
                return new AppStatus(_currentVersion, _latestVersion);
            }
        }
    }

    public bool UpdateCurrentVersion(string? version, out AppStatus status) =>
        Update(version, ref _currentVersion, out status);

    public bool UpdateLatestVersion(string? version, out AppStatus status) =>
        Update(version, ref _latestVersion, out status);

    private bool Update(string? value, ref string? target, out AppStatus status)
    {
        lock (_sync)
        {
            if (AreEqual(target, value))
            {
                status = new AppStatus(_currentVersion, _latestVersion);
                return false;
            }

            target = value;
            status = new AppStatus(_currentVersion, _latestVersion);
            return true;
        }
    }

    private static bool AreEqual(string? left, string? right) =>
        string.Equals(left, right, StringComparison.Ordinal);
}

using Cleanuparr.Shared.Configuration;
using Cleanuparr.Shared.Helpers;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Utilities;

public class ConfigurationPathProviderTests : IDisposable
{
    private readonly string? _originalLogsPath;

    public ConfigurationPathProviderTests()
    {
        _originalLogsPath = Environment.GetEnvironmentVariable(ConfigurationKeys.LogsPath);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.LogsPath, _originalLogsPath);
    }

    [Fact]
    public void GetLogPath_ReturnsOverride_WhenEnvVarSet()
    {
        string expected = Path.Combine(Path.GetTempPath(), "cleanuparr-test-logs");
        Environment.SetEnvironmentVariable(ConfigurationKeys.LogsPath, expected);

        ConfigurationPathProvider.GetLogPath().ShouldBe(expected);
    }

    [Fact]
    public void GetLogPath_ReturnsConfigSubdirectory_WhenEnvVarUnset()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.LogsPath, null);

        string expected = Path.Combine(ConfigurationPathProvider.GetConfigPath(), "logs");
        ConfigurationPathProvider.GetLogPath().ShouldBe(expected);
    }
}

using Cleanuparr.Shared.Helpers;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Utilities;

public class ConfigurationPathProviderTests
{
    [Fact]
    public void ResolveConfigPath_ReturnsOverride_WhenSetOutsideContainer()
    {
        string expected = Path.Combine(Path.GetTempPath(), "cleanuparr-test-cfg");

        ConfigurationPathProvider.ResolveConfigPath(expected, isInContainer: false).ShouldBe(expected);
    }

    [Fact]
    public void ResolveConfigPath_IgnoresOverride_WhenInContainer()
    {
        string ignored = Path.Combine(Path.GetTempPath(), "cleanuparr-test-cfg");

        ConfigurationPathProvider.ResolveConfigPath(ignored, isInContainer: true).ShouldBe("/config");
    }

    [Fact]
    public void ResolveConfigPath_ReturnsContainerDefault_WhenNoOverrideInContainer()
    {
        ConfigurationPathProvider.ResolveConfigPath(null, isInContainer: true).ShouldBe("/config");
    }

    [Fact]
    public void ResolveConfigPath_ReturnsLocalDefault_WhenNoOverrideOutsideContainer()
    {
        string expected = Path.Combine(AppContext.BaseDirectory, "config");

        ConfigurationPathProvider.ResolveConfigPath(null, isInContainer: false).ShouldBe(expected);
    }

    [Fact]
    public void ResolveLogPath_ReturnsOverride_WhenSetOutsideContainer()
    {
        string expected = Path.Combine(Path.GetTempPath(), "cleanuparr-test-logs");

        ConfigurationPathProvider.ResolveLogPath(expected, "/anything", isInContainer: false).ShouldBe(expected);
    }

    [Fact]
    public void ResolveLogPath_IgnoresOverride_WhenInContainer()
    {
        string ignored = Path.Combine(Path.GetTempPath(), "cleanuparr-test-logs");

        ConfigurationPathProvider.ResolveLogPath(ignored, "/config", isInContainer: true)
            .ShouldBe(Path.Combine("/config", "logs"));
    }

    [Fact]
    public void ResolveLogPath_ReturnsConfigSubdirectory_WhenNoOverride()
    {
        string configPath = Path.Combine(Path.GetTempPath(), "cleanuparr-test-cfg");

        ConfigurationPathProvider.ResolveLogPath(null, configPath, isInContainer: false)
            .ShouldBe(Path.Combine(configPath, "logs"));
    }
}

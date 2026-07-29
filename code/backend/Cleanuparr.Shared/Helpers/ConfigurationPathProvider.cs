using Cleanuparr.Shared.Configuration;

namespace Cleanuparr.Shared.Helpers;

/// <summary>
/// Provides the appropriate configuration path based on the runtime environment.
/// Uses '/config' for Docker containers and a relative 'config' path for normal environments.
/// </summary>
public static class ConfigurationPathProvider
{
    private static string? _configPath;
    
    static ConfigurationPathProvider()
    {
        try
        {
            string configPath = InitializeConfigPath();
            
            if (!Directory.Exists(configPath))
            {
                Directory.CreateDirectory(configPath);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create configuration directories: {ex.Message}", ex);
        }
    }

    private static string InitializeConfigPath()
    {
        string? overridePath = Environment.GetEnvironmentVariable(ConfigurationKeys.ConfigPath);
        _configPath = ResolveConfigPath(overridePath, IsInContainer());
        return _configPath;
    }

    internal static string ResolveConfigPath(string? overridePath, bool isInContainer)
    {
        if (!isInContainer && !string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        if (isInContainer)
        {
            return "/config";
        }

        return Path.Combine(AppContext.BaseDirectory, "config");
    }

    internal static string ResolveLogPath(string? overridePath, string configPath, bool isInContainer)
    {
        if (!isInContainer && !string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        return Path.Combine(configPath, "logs");
    }

    private static bool IsInContainer()
    {
        return Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
    }

    public const string ConfigFileName = "cleanuparr.json";

    public static string GetConfigPath()
    {
        return _configPath ?? InitializeConfigPath();
    }

    public static string GetConfigFilePath()
    {
        return Path.Combine(GetConfigPath(), ConfigFileName);
    }

    /// <summary>
    /// Gets the log directory.
    /// </summary>
    /// <returns>
    /// The override directory when the user sets it outside Docker.
    /// If the user does not set it, a logs folder in the configuration directory.
    /// </returns>
    public static string GetLogPath()
    {
        string? overridePath = Environment.GetEnvironmentVariable(ConfigurationKeys.LogsPath);
        return ResolveLogPath(overridePath, GetConfigPath(), IsInContainer());
    }

    public static void SetConfigPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(path));
        }
        
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        _configPath = path;
    }
}

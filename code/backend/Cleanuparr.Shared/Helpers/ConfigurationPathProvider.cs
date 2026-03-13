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
        // Check if running in Docker container
        bool isInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
        
        if (isInContainer)
        {
            // Use absolute path for Docker
            _configPath = "/config";
        }
        else
        {
            // Use path relative to app for normal environment
            _configPath = Path.Combine(AppContext.BaseDirectory, "config");
        }

        return _configPath;
    }
    
    public static string GetConfigPath()
    {
        return _configPath ?? InitializeConfigPath();
    }

    public static void SetConfigPath(string path)
    {
        _configPath = path;

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}

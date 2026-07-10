using Cleanuparr.Shared.Configuration;
using Cleanuparr.Shared.Enums;
using Microsoft.Extensions.Configuration;

namespace Cleanuparr.Shared.Helpers;

public static class DatabaseConfigProvider
{
    private static IConfiguration? _configuration;

    public static void Initialize(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public static DatabaseProvider Provider => ParseProvider(Read(ConfigurationKeys.DatabaseProvider));

    public static DatabaseProvider ParseProvider(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DatabaseProvider.Sqlite;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "sqlite" => DatabaseProvider.Sqlite,
            "postgres" => DatabaseProvider.Postgres,
            _ => throw new InvalidOperationException($"Unsupported {ConfigurationKeys.DatabaseProvider} value: '{value}'. Supported values: sqlite, postgres."),
        };
    }

    public static string GetRequired(string key) =>
        Read(key)
        ?? throw new InvalidOperationException($"Configuration value '{key}' is required when {ConfigurationKeys.DatabaseProvider}=postgres.");

    public static string? GetOptional(string key) => Read(key);

    private static string? Read(string key)
    {
        string? value = _configuration is not null ? _configuration[key] : Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

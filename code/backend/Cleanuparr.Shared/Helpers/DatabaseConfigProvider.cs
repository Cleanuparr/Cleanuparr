using Cleanuparr.Shared.Enums;

namespace Cleanuparr.Shared.Helpers;

public static class DatabaseConfigProvider
{
    public static DatabaseProvider Provider => ParseProvider(Environment.GetEnvironmentVariable("DATABASE_PROVIDER"));

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
            _ => throw new InvalidOperationException($"Unsupported DATABASE_PROVIDER value: '{value}'. Supported values: sqlite, postgres."),
        };
    }

    public static string GetRequired(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Environment variable '{name}' is required when DATABASE_PROVIDER=postgres.");

    public static string? GetOptional(string name) => Environment.GetEnvironmentVariable(name);
}

namespace Cleanuparr.Shared.Configuration;

public static class ConfigurationKeys
{
    public const string DatabaseProvider = "DATABASE_PROVIDER";
    public const string PostgresHost = "POSTGRES_HOST";
    public const string PostgresPort = "POSTGRES_PORT";
    public const string PostgresUser = "POSTGRES_USER";
    public const string PostgresPassword = "POSTGRES_PASS";
    public const string PostgresDatabase = "POSTGRES_DB";
    public const string PostgresExtraParams = "POSTGRES_EXTRA_PARAMS";
    public const string Port = "PORT";
    public const string BindAddress = "BIND_ADDRESS";
    public const string BasePath = "BASE_PATH";
    /// <summary>
    /// Sets the configuration directory. This applies outside Docker only.
    /// </summary>
    public const string ConfigPath = "CLEANUPARR_CONFIG_PATH";

    /// <summary>
    /// Sets the log directory. This applies outside Docker only.
    /// </summary>
    public const string LogsPath = "CLEANUPARR_LOGS_PATH";
}

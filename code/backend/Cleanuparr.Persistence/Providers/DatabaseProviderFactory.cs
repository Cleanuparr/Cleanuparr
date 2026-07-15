using Cleanuparr.Shared.Enums;
using Cleanuparr.Shared.Helpers;

namespace Cleanuparr.Persistence.Providers;

public static class DatabaseProviderFactory
{
    public static IDatabaseProvider Current =>
        DatabaseConfigProvider.Provider switch
        {
            DatabaseProvider.Sqlite => new SqliteDatabaseProvider(),
            DatabaseProvider.Postgres => new PostgresDatabaseProvider(),
            _ => throw new InvalidOperationException($"No provider registered for {DatabaseConfigProvider.Provider}."),
        };
}

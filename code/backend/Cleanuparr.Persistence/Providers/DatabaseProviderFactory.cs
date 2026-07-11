using Cleanuparr.Shared.Enums;
using Cleanuparr.Shared.Helpers;

namespace Cleanuparr.Persistence.Providers;

public static class DatabaseProviderFactory
{
    private static readonly Lazy<IDatabaseProvider> LazyDefault = new(Create);

    private static IDatabaseProvider? _override;

    public static IDatabaseProvider Current => _override ?? LazyDefault.Value;

    internal static void SetOverrideForTesting(IDatabaseProvider? provider)
    {
        _override = provider;
    }

    private static IDatabaseProvider Create()
    {
        return DatabaseConfigProvider.Provider switch
        {
            DatabaseProvider.Sqlite => new SqliteDatabaseProvider(),
            DatabaseProvider.Postgres => new PostgresDatabaseProvider(),
            _ => throw new InvalidOperationException($"No provider registered for {DatabaseConfigProvider.Provider}."),
        };
    }
}

using Cleanuparr.Shared.Enums;
using Cleanuparr.Shared.Helpers;

namespace Cleanuparr.Persistence.Providers;

public static class DatabaseProviderFactory
{
    private static readonly Lazy<IDatabaseProvider> LazyCurrent = new(Create);

    public static IDatabaseProvider Current => LazyCurrent.Value;

    private static IDatabaseProvider Create()
    {
        return DatabaseConfigProvider.Provider switch
        {
            DatabaseProvider.Sqlite => new SqliteDatabaseProvider(),
            _ => throw new NotSupportedException("PostgreSQL provider is added in a later task."),
        };
    }
}

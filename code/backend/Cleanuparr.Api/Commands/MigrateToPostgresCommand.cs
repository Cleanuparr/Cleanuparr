using Cleanuparr.Infrastructure.Features.DatabaseMigration;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Configuration;

namespace Cleanuparr.Api.Commands;

public static class MigrateToPostgresCommand
{
    public const string Name = "migrate-to-postgres";
    public const string ForceFlag = "--force";

    public static bool Matches(string[] args) => args.Length > 0 && args[0] == Name;

    public static async Task<int> RunAsync(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile(ConfigurationPathProvider.GetConfigFilePath(), optional: true)
            .AddEnvironmentVariables()
            .Build();
        DatabaseConfigProvider.Initialize(configuration);

        bool force = args.Contains(ForceFlag);
        SqliteToPostgresMigrator migrator = new();
        MigrationResult result = await migrator.RunAsync(force, new ConsoleProgress(), CancellationToken.None);

        if (!result.Success)
        {
            Console.Error.WriteLine($"Migration failed: {result.Error}");
            return 1;
        }

        Console.WriteLine("Migration complete. Row counts:");
        foreach (KeyValuePair<string, int> entry in result.TableCounts.OrderBy(pair => pair.Key))
        {
            Console.WriteLine($"  {entry.Key}: {entry.Value}");
        }

        Console.WriteLine("Set DATABASE_PROVIDER=postgres and restart Cleanuparr to run on PostgreSQL.");
        Console.WriteLine("(Your SQLite databases were upgraded to the current schema in place; their data was preserved.)");
        return 0;
    }

    private sealed class ConsoleProgress : IProgress<string>
    {
        public void Report(string value) => Console.WriteLine($"  {value}");
    }
}

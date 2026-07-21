using Cleanuparr.Shared.Configuration;
using Cleanuparr.Shared.Enums;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Providers;

public class DatabaseConfigProviderTests
{
    [Fact]
    public void ParseProvider_null_defaults_to_sqlite()
    {
        DatabaseConfigProvider.ParseProvider(null).ShouldBe(DatabaseProvider.Sqlite);
    }

    [Fact]
    public void ParseProvider_empty_defaults_to_sqlite()
    {
        DatabaseConfigProvider.ParseProvider("   ").ShouldBe(DatabaseProvider.Sqlite);
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("Postgres")]
    [InlineData("POSTGRES")]
    public void ParseProvider_reads_postgres_case_insensitively(string value)
    {
        DatabaseConfigProvider.ParseProvider(value).ShouldBe(DatabaseProvider.Postgres);
    }

    [Fact]
    public void ParseProvider_unknown_value_throws()
    {
        Should.Throw<InvalidOperationException>(() => DatabaseConfigProvider.ParseProvider("mysql"));
    }

    [Fact]
    public void Provider_reads_from_configuration_when_initialized()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ConfigurationKeys.DatabaseProvider] = "postgres" })
            .Build();
        try
        {
            DatabaseConfigProvider.Initialize(config);
            DatabaseConfigProvider.Provider.ShouldBe(DatabaseProvider.Postgres);
        }
        finally
        {
            DatabaseConfigProvider.Initialize(new ConfigurationBuilder().Build());
        }
    }

    [Fact]
    public void GetRequired_reads_value_from_configuration()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ConfigurationKeys.PostgresHost] = "db.example" })
            .Build();
        try
        {
            DatabaseConfigProvider.Initialize(config);
            DatabaseConfigProvider.GetRequired(ConfigurationKeys.PostgresHost).ShouldBe("db.example");
        }
        finally
        {
            DatabaseConfigProvider.Initialize(new ConfigurationBuilder().Build());
        }
    }
}

using Cleanuparr.Shared.Enums;
using Cleanuparr.Shared.Helpers;
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
}

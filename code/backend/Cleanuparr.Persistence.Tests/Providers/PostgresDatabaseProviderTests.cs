using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Providers;
using Cleanuparr.Shared.Enums;
using Npgsql;
using Shouldly;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Providers;

public class PostgresDatabaseProviderTests
{
    private readonly PostgresDatabaseProvider _provider = new();

    [Fact]
    public void Kind_is_postgres()
    {
        _provider.Kind.ShouldBe(DatabaseProvider.Postgres);
    }

    [Fact]
    public void GetSchema_maps_each_context()
    {
        _provider.GetSchema(DbContextKind.Data).ShouldBe("data");
        _provider.GetSchema(DbContextKind.Events).ShouldBe("events");
        _provider.GetSchema(DbContextKind.Users).ShouldBe("users");
    }

    [Fact]
    public void GetUnresolvedEventFilter_uses_false()
    {
        _provider.GetUnresolvedEventFilter().ShouldBe("is_resolved = false");
    }

    [Fact]
    public void BuildConnectionString_from_discrete_values()
    {
        string result = PostgresDatabaseProvider.BuildConnectionString("db-host", "5432", "cleanuparr", "s3cret", "cleanuparr", null);
        NpgsqlConnectionStringBuilder parsed = new(result);
        parsed.Host.ShouldBe("db-host");
        parsed.Port.ShouldBe(5432);
        parsed.Username.ShouldBe("cleanuparr");
        parsed.Password.ShouldBe("s3cret");
        parsed.Database.ShouldBe("cleanuparr");
    }

    [Fact]
    public void BuildConnectionString_escapes_special_password()
    {
        string result = PostgresDatabaseProvider.BuildConnectionString("h", "5432", "u", "pa;ss=word", "d", null);
        NpgsqlConnectionStringBuilder parsed = new(result);
        parsed.Password.ShouldBe("pa;ss=word");
    }

    [Fact]
    public void BuildConnectionString_merges_extra_params()
    {
        string result = PostgresDatabaseProvider.BuildConnectionString("h", "5432", "u", "p", "d", "SSL Mode=Require;Minimum Pool Size=5");
        NpgsqlConnectionStringBuilder parsed = new(result);
        parsed.SslMode.ShouldBe(SslMode.Require);
        parsed.MinPoolSize.ShouldBe(5);
    }

    [Fact]
    public void BuildConnectionString_invalid_extra_params_throws()
    {
        Should.Throw<Exception>(() =>
            PostgresDatabaseProvider.BuildConnectionString("h", "5432", "u", "p", "d", "this is not valid"));
    }

    [Fact]
    public void GetTimelineBucketExpr_month_uses_date_trunc_in_utc()
    {
        _provider.GetTimelineBucketExpr(TimelineBucketSize.Month)
            .ShouldBe("to_char(date_trunc('month', \"timestamp\" AT TIME ZONE 'UTC'), 'YYYY-MM-DD')");
    }
}

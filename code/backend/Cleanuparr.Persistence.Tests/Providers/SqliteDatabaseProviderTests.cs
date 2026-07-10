using Cleanuparr.Persistence.Providers;
using Cleanuparr.Shared.Enums;
using Shouldly;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Providers;

public class SqliteDatabaseProviderTests
{
    private readonly SqliteDatabaseProvider _provider = new();

    [Fact]
    public void Kind_is_sqlite()
    {
        _provider.Kind.ShouldBe(DatabaseProvider.Sqlite);
    }

    [Theory]
    [InlineData(DbContextKind.Data)]
    [InlineData(DbContextKind.Events)]
    [InlineData(DbContextKind.Users)]
    public void GetSchema_is_null_for_sqlite(DbContextKind kind)
    {
        _provider.GetSchema(kind).ShouldBeNull();
    }

    [Fact]
    public void GetUnresolvedEventFilter_uses_zero()
    {
        _provider.GetUnresolvedEventFilter().ShouldBe("\"is_resolved\" = 0");
    }

    [Fact]
    public void EscapeLikePattern_escapes_wildcards_with_backslash_and_wraps()
    {
        _provider.EscapeLikePattern("a_b%c[d").ShouldBe("%a\\_b\\%c[d%");
    }
}

using Cleanuparr.Persistence.Startup;
using Shouldly;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Startup;

public sealed class UserInfoMigrationTests
{
    #region ParseCredential

    [Fact]
    public void ParseCredential_UsernameAndPassword_SplitsOnFirstColon()
    {
        UserInfoCredential credential = UserInfoMigration.ParseCredential("user:pass");

        credential.Username.ShouldBe("user");
        credential.Password.ShouldBe("pass");
    }

    [Fact]
    public void ParseCredential_NoColon_IsUsernameOnly()
    {
        UserInfoCredential credential = UserInfoMigration.ParseCredential("user");

        credential.Username.ShouldBe("user");
        credential.Password.ShouldBeNull();
    }

    [Fact]
    public void ParseCredential_EmptyUsername_DecodesToNull()
    {
        UserInfoCredential credential = UserInfoMigration.ParseCredential(":pass");

        credential.Username.ShouldBeNull();
        credential.Password.ShouldBe("pass");
    }

    [Fact]
    public void ParseCredential_PasswordWithColon_KeepsItInThePassword()
    {
        UserInfoCredential credential = UserInfoMigration.ParseCredential("u:p:q");

        credential.Username.ShouldBe("u");
        credential.Password.ShouldBe("p:q");
    }

    [Fact]
    public void ParseCredential_EncodedSpecialCharacters_AreDecoded()
    {
        UserInfoCredential credential = UserInfoMigration.ParseCredential("u%3Ap:p%25q");

        credential.Username.ShouldBe("u:p");
        credential.Password.ShouldBe("p%q");
    }

    [Fact]
    public void ParseCredential_EncodedSlashSpaceAndAt_AreDecoded()
    {
        UserInfoCredential credential = UserInfoMigration.ParseCredential("us%2Fer:p%40ss%20word");

        credential.Username.ShouldBe("us/er");
        credential.Password.ShouldBe("p@ss word");
    }

    [Fact]
    public void ParseCredential_NonAsciiEncoded_IsDecoded()
    {
        UserInfoCredential credential = UserInfoMigration.ParseCredential("u:caf%C3%A9");

        credential.Password.ShouldBe("café");
    }

    #endregion

    #region StripUserInfo

    [Theory]
    [InlineData("http://user:pass@host:8080/path/?q=1", "http://host:8080/path/?q=1")]
    [InlineData("http://user:pass@host/path/", "http://host/path/")]
    [InlineData("http://user:pass@host", "http://host")]
    [InlineData("https://user:pass@HOST:443/", "https://HOST:443/")]
    [InlineData("HTTP://user:pass@host:80/", "HTTP://host:80/")]
    [InlineData("http://u:p@host:1234/a/b/", "http://host:1234/a/b/")]
    [InlineData("http://u%3Ap:p%25q@host/a%20b?x=%2F", "http://host/a%20b?x=%2F")]
    [InlineData("http://user:pass@[::1]:8080/path", "http://[::1]:8080/path")]
    public void StripUserInfo_RemovesOnlyTheUserinfo(string input, string expected)
    {
        Uri host = new(input, UriKind.RelativeOrAbsolute);

        Uri stripped = UserInfoMigration.StripUserInfo(host);

        stripped.OriginalString.ShouldBe(expected);
    }

    [Fact]
    public void StripUserInfo_NoAtSign_ReturnsHostUnchanged()
    {
        Uri host = new("http://host/path");

        Uri stripped = UserInfoMigration.StripUserInfo(host);

        stripped.ShouldBe(host);
    }

    #endregion
}

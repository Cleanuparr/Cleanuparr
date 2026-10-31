using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Persistence.Models.Configuration;
using Shouldly;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Models.Configuration;

public sealed class DownloadClientConfigTests
{
    #region Validate - Valid Configurations

    [Fact]
    public void Validate_WithValidConfig_DoesNotThrow()
    {
        var config = new DownloadClientConfig
        {
            Name = "My qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080")
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithHttpsHost_DoesNotThrow()
    {
        var config = new DownloadClientConfig
        {
            Name = "Remote Client",
            TypeName = DownloadClientTypeName.Transmission,
            Type = DownloadClientType.Torrent,
            Host = new Uri("https://remote.example.com:9091")
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - Name Validation

    [Fact]
    public void Validate_WithEmptyName_ThrowsValidationException()
    {
        var config = new DownloadClientConfig
        {
            Name = "",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080")
        };

        var exception = Should.Throw<CustomValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Client name cannot be empty");
    }

    [Fact]
    public void Validate_WithWhitespaceName_ThrowsValidationException()
    {
        var config = new DownloadClientConfig
        {
            Name = "   ",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080")
        };

        var exception = Should.Throw<CustomValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Client name cannot be empty");
    }

    [Fact]
    public void Validate_WithTabOnlyName_ThrowsValidationException()
    {
        var config = new DownloadClientConfig
        {
            Name = "\t",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080")
        };

        var exception = Should.Throw<CustomValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Client name cannot be empty");
    }

    #endregion

    #region Validate - Host Validation

    [Fact]
    public void Validate_WithNullHost_ThrowsValidationException()
    {
        var config = new DownloadClientConfig
        {
            Name = "My Client",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = null
        };

        var exception = Should.Throw<CustomValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Host cannot be empty");
    }

    [Fact]
    public void Validate_WithUserInfoInHost_ThrowsValidationException()
    {
        var config = new DownloadClientConfig
        {
            Name = "My Client",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://user:pass@localhost:8080")
        };

        var exception = Should.Throw<CustomValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Credentials in the URL are not supported. Use the HTTP authentication fields instead.");
    }

    [Fact]
    public void Validate_WithRelativeHost_DoesNotThrow()
    {
        var config = new DownloadClientConfig
        {
            Name = "My Client",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("192.168.1.5", UriKind.RelativeOrAbsolute)
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - Directory Mapping

    [Fact]
    public void Validate_WithOnlyDownloadDirectorySourceSet_ThrowsValidationException()
    {
        var config = new DownloadClientConfig
        {
            Name = "My Client",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            DownloadDirectorySource = "/downloads",
            DownloadDirectoryTarget = null
        };

        var exception = Should.Throw<CustomValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Both download directory source and target must be set, or both must be empty");
    }

    [Fact]
    public void Validate_WithOnlyDownloadDirectoryTargetSet_ThrowsValidationException()
    {
        var config = new DownloadClientConfig
        {
            Name = "My Client",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            DownloadDirectorySource = null,
            DownloadDirectoryTarget = "/data/downloads"
        };

        var exception = Should.Throw<CustomValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Both download directory source and target must be set, or both must be empty");
    }

    [Fact]
    public void Validate_WithBothDownloadDirectoriesSet_DoesNotThrow()
    {
        var config = new DownloadClientConfig
        {
            Name = "My Client",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            DownloadDirectorySource = "/downloads",
            DownloadDirectoryTarget = "/data/downloads"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithBothDownloadDirectoriesEmpty_DoesNotThrow()
    {
        var config = new DownloadClientConfig
        {
            Name = "My Client",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            DownloadDirectorySource = null,
            DownloadDirectoryTarget = null
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - API Key Validation

    [Fact]
    public void Validate_SabnzbdWithEmptyApiKey_ThrowsValidationException()
    {
        var config = new DownloadClientConfig
        {
            Name = "My Sabnzbd",
            TypeName = DownloadClientTypeName.Sabnzbd,
            Type = DownloadClientType.Usenet,
            Host = new Uri("http://localhost:8080"),
            ApiKey = null
        };

        var exception = Should.Throw<CustomValidationException>(() => config.Validate());
        exception.Message.ShouldBe("API key cannot be empty");
    }

    [Fact]
    public void Validate_SabnzbdWithApiKey_DoesNotThrow()
    {
        var config = new DownloadClientConfig
        {
            Name = "My Sabnzbd",
            TypeName = DownloadClientTypeName.Sabnzbd,
            Type = DownloadClientType.Usenet,
            Host = new Uri("http://localhost:8080"),
            ApiKey = "a-real-key"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_QBittorrentWithEmptyApiKey_DoesNotThrow()
    {
        var config = new DownloadClientConfig
        {
            Name = "My qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            ApiKey = null
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_QBittorrentWithApiKeyOnly_DoesNotThrow()
    {
        var config = new DownloadClientConfig
        {
            Name = "My qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            ApiKey = "qbt_abcdefghijklmnopqrstuvwxyz12"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_QBittorrentWithApiKeyAndPassword_ThrowsValidationException()
    {
        var config = new DownloadClientConfig
        {
            Name = "My qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            ApiKey = "qbt_abcdefghijklmnopqrstuvwxyz12",
            Password = "pass"
        };

        var exception = Should.Throw<CustomValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Use either an API key or a username and password, not both");
    }

    [Fact]
    public void Validate_QBittorrentWithApiKeyAndUsername_ThrowsValidationException()
    {
        var config = new DownloadClientConfig
        {
            Name = "My qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            ApiKey = "qbt_abcdefghijklmnopqrstuvwxyz12",
            Username = "admin"
        };

        var exception = Should.Throw<CustomValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Use either an API key or a username and password, not both");
    }

    [Fact]
    public void Validate_SabnzbdWithApiKeyAndPassword_DoesNotThrow()
    {
        var config = new DownloadClientConfig
        {
            Name = "My Sabnzbd",
            TypeName = DownloadClientTypeName.Sabnzbd,
            Type = DownloadClientType.Usenet,
            Host = new Uri("http://localhost:8080"),
            ApiKey = "a-real-key",
            Password = "unused"
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - HTTP Auth

    [Fact]
    public void Validate_NewConfig_DefaultsHttpAuthTypeToBasic()
    {
        var config = new DownloadClientConfig
        {
            Name = "My qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080")
        };

        config.HttpAuthType.ShouldBe(HttpAuthType.Basic);
    }

    [Fact]
    public void Validate_QBittorrentWithApiKeyAndHttpAuthUsername_ThrowsValidationException()
    {
        var config = new DownloadClientConfig
        {
            Name = "My qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            ApiKey = "qbt_abcdefghijklmnopqrstuvwxyz12",
            HttpAuthUsername = "proxyuser"
        };

        var exception = Should.Throw<CustomValidationException>(() => config.Validate());
        exception.Message.ShouldBe("API key can't be combined with HTTP authentication. Use qBittorrent username/password instead.");
    }

    [Fact]
    public void Validate_SabnzbdWithApiKeyAndHttpAuthUsername_DoesNotThrow()
    {
        var config = new DownloadClientConfig
        {
            Name = "My SABnzbd",
            TypeName = DownloadClientTypeName.Sabnzbd,
            Type = DownloadClientType.Usenet,
            Host = new Uri("http://localhost:8080"),
            ApiKey = "0123456789abcdef0123456789abcdef",
            HttpAuthUsername = "proxyuser"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_QBittorrentWithApiKeyAndNoHttpAuthUsername_DoesNotThrow()
    {
        var config = new DownloadClientConfig
        {
            Name = "My qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            ApiKey = "qbt_abcdefghijklmnopqrstuvwxyz12",
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_DigestWithoutUsername_ThrowsValidationException()
    {
        var config = new DownloadClientConfig
        {
            Name = "My rTorrent",
            TypeName = DownloadClientTypeName.rTorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            HttpAuthType = HttpAuthType.Digest,
        };

        var exception = Should.Throw<CustomValidationException>(() => config.Validate());
        exception.Message.ShouldBe("HTTP Digest authentication requires a username");
    }

    [Fact]
    public void Validate_DigestWithUsername_DoesNotThrow()
    {
        var config = new DownloadClientConfig
        {
            Name = "My rTorrent",
            TypeName = DownloadClientTypeName.rTorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            HttpAuthType = HttpAuthType.Digest,
            Username = "proxyuser",
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_DigestQBittorrentWithoutHttpAuthUsername_ThrowsValidationException()
    {
        var config = new DownloadClientConfig
        {
            Name = "My qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            HttpAuthType = HttpAuthType.Digest,
            Username = "qbit-login-user",
        };

        var exception = Should.Throw<CustomValidationException>(() => config.Validate());
        exception.Message.ShouldBe("HTTP Digest authentication requires a username");
    }

    [Fact]
    public void Validate_DigestQBittorrentWithHttpAuthUsername_DoesNotThrow()
    {
        var config = new DownloadClientConfig
        {
            Name = "My qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            HttpAuthType = HttpAuthType.Digest,
            HttpAuthUsername = "proxyuser",
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region GetHandlerHttpAuthCredential

    [Fact]
    public void GetHandlerHttpAuthCredential_BasicRTorrent_ReturnsNull()
    {
        var config = new DownloadClientConfig
        {
            Name = "My rTorrent",
            TypeName = DownloadClientTypeName.rTorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            HttpAuthType = HttpAuthType.Basic,
            Username = "user",
            Password = "pass",
        };

        config.GetHandlerHttpAuthCredential().ShouldBeNull();
    }

    [Fact]
    public void GetHandlerHttpAuthCredential_DigestRTorrent_UsesUsernamePassword()
    {
        var config = new DownloadClientConfig
        {
            Name = "My rTorrent",
            TypeName = DownloadClientTypeName.rTorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            HttpAuthType = HttpAuthType.Digest,
            Username = "user",
            Password = "pass",
        };

        var credential = config.GetHandlerHttpAuthCredential();
        credential.ShouldNotBeNull();
        credential!.UserName.ShouldBe("user");
        credential.Password.ShouldBe("pass");
    }

    [Fact]
    public void GetHandlerHttpAuthCredential_BasicQBittorrentWithHttpAuthUsername_UsesDedicatedFields()
    {
        var config = new DownloadClientConfig
        {
            Name = "My qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            HttpAuthType = HttpAuthType.Basic,
            Username = "qbit-login",
            Password = "qbit-pass",
            HttpAuthUsername = "proxyuser",
            HttpAuthPassword = "proxypass",
        };

        var credential = config.GetHandlerHttpAuthCredential();
        credential.ShouldNotBeNull();
        credential!.UserName.ShouldBe("proxyuser");
        credential.Password.ShouldBe("proxypass");
    }

    [Fact]
    public void GetHandlerHttpAuthCredential_BasicQBittorrentWithoutHttpAuthUsername_ReturnsNull()
    {
        var config = new DownloadClientConfig
        {
            Name = "My qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            HttpAuthType = HttpAuthType.Basic,
            Username = "qbit-login",
            Password = "qbit-pass",
        };

        config.GetHandlerHttpAuthCredential().ShouldBeNull();
    }

    #endregion

    #region Url Property Tests

    [Fact]
    public void Url_WithHostAndNoUrlBase_ReturnsHostWithTrailingSlash()
    {
        var config = new DownloadClientConfig
        {
            Name = "My Client",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            UrlBase = null
        };

        config.Url.ToString().ShouldBe("http://localhost:8080/");
    }

    [Fact]
    public void Url_WithHostAndUrlBase_ReturnsCombinedUrl()
    {
        var config = new DownloadClientConfig
        {
            Name = "My Client",
            TypeName = DownloadClientTypeName.Transmission,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:9091"),
            UrlBase = "transmission/rpc"
        };

        config.Url.ToString().ShouldBe("http://localhost:9091/transmission/rpc");
    }

    [Fact]
    public void Url_WithUrlBaseWithLeadingSlash_TrimsLeadingSlash()
    {
        var config = new DownloadClientConfig
        {
            Name = "My Client",
            TypeName = DownloadClientTypeName.Deluge,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8112"),
            UrlBase = "/json"
        };

        config.Url.ToString().ShouldBe("http://localhost:8112/json");
    }

    [Fact]
    public void Url_WithUrlBaseWithTrailingSlash_TrimsTrailingSlash()
    {
        var config = new DownloadClientConfig
        {
            Name = "My Client",
            TypeName = DownloadClientTypeName.Transmission,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:9091"),
            UrlBase = "transmission/rpc/"
        };

        config.Url.ToString().ShouldBe("http://localhost:9091/transmission/rpc");
    }

    [Fact]
    public void Url_WithHostTrailingSlash_HandlesCorrectly()
    {
        var config = new DownloadClientConfig
        {
            Name = "My Client",
            TypeName = DownloadClientTypeName.Transmission,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:9091/"),
            UrlBase = "transmission/rpc"
        };

        config.Url.ToString().ShouldBe("http://localhost:9091/transmission/rpc");
    }

    #endregion
}

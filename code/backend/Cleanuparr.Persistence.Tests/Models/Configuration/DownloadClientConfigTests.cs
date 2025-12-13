using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

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

        var exception = Should.Throw<ValidationException>(() => config.Validate());
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

        var exception = Should.Throw<ValidationException>(() => config.Validate());
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

        var exception = Should.Throw<ValidationException>(() => config.Validate());
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

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Host cannot be empty");
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

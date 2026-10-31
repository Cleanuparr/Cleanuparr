using Cleanuparr.Api.Features.DownloadClient.Contracts.Requests;
using Cleanuparr.Api.Features.DownloadClient.Contracts.Responses;
using Cleanuparr.Api.Features.DownloadClient.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Entities.HealthCheck;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Shared.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Features.DownloadClient;

public class DownloadClientControllerTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly IDynamicHttpClientFactory _dynamicHttpClientFactory;
    private readonly IDownloadServiceFactory _downloadServiceFactory;
    private readonly DownloadClientController _controller;

    public DownloadClientControllerTests()
    {
        _dataContext = ConfigControllerTestDataFactory.CreateDataContext();
        var logger = Substitute.For<ILogger<DownloadClientController>>();
        _dynamicHttpClientFactory = Substitute.For<IDynamicHttpClientFactory>();
        _downloadServiceFactory = Substitute.For<IDownloadServiceFactory>();
        _controller = new DownloadClientController(logger, _dataContext, _dynamicHttpClientFactory, _downloadServiceFactory);
        ConfigControllerTestDataFactory.ConfigureProblemDetails(_controller);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetDownloadClientConfig_EmptyDatabase_ReturnsOkWithEmptyClients()
    {
        // Act
        var result = await _controller.GetDownloadClientConfig();

        // Assert
        var ok = result.ShouldBeOfType<OkObjectResult>();
        ok.Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetDownloadClientConfig_OrdersByTypeThenName()
    {
        // Arrange — add 3 clients out of order
        _dataContext.DownloadClients.AddRange(
            NewClient("z-client", DownloadClientTypeName.qBittorrent),
            NewClient("a-client", DownloadClientTypeName.qBittorrent),
            NewClient("b-client", DownloadClientTypeName.Deluge)
        );
        await _dataContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetDownloadClientConfig();

        // Assert
        var ok = result.ShouldBeOfType<OkObjectResult>();
        var dict = ok.Value!.GetType().GetProperty("clients")!.GetValue(ok.Value) as List<DownloadClientConfigResponse>;
        dict.ShouldNotBeNull();
        dict!.Count.ShouldBe(3);
        // qBittorrent (0) comes before Deluge (1) by enum value, then alphabetical within type
        dict![0].Name.ShouldBe("a-client");
        dict![1].Name.ShouldBe("z-client");
        dict![2].Name.ShouldBe("b-client");
    }

    [Fact]
    public async Task CreateDownloadClientConfig_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new CreateDownloadClientRequest
        {
            Enabled = true,
            Name = "my-client",
            TypeName = DownloadClientTypeName.qBittorrent,
            Host = "http://localhost:8080",
            Username = "user",
            Password = "pass",
        };

        // Act
        var result = await _controller.CreateDownloadClientConfig(request);

        // Assert
        var created = result.ShouldBeOfType<CreatedAtActionResult>();
        var entity = created.Value.ShouldBeOfType<DownloadClientConfigResponse>();
        entity.Name.ShouldBe("my-client");
        (await _dataContext.DownloadClients.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task CreateDownloadClientConfig_QBittorrentWithApiKey_PersistsApiKey()
    {
        // Arrange
        var request = new CreateDownloadClientRequest
        {
            Enabled = true,
            Name = "qbit-key-client",
            TypeName = DownloadClientTypeName.qBittorrent,
            Host = "http://localhost:8080",
            ApiKey = "qbt_abcdefghijklmnopqrstuvwxyz12",
        };

        // Act
        await _controller.CreateDownloadClientConfig(request);

        // Assert
        var saved = await _dataContext.DownloadClients.AsNoTracking().FirstAsync(c => c.Name == "qbit-key-client");
        saved.ApiKey.ShouldBe("qbt_abcdefghijklmnopqrstuvwxyz12");
        saved.Username.ShouldBeNull();
        saved.Password.ShouldBeNull();
    }

    [Fact]
    public async Task CreateDownloadClientConfig_QBittorrentWithApiKeyAndPassword_PropagatesValidationException()
    {
        // Arrange
        var request = new CreateDownloadClientRequest
        {
            Enabled = true,
            Name = "qbit-conflict-client",
            TypeName = DownloadClientTypeName.qBittorrent,
            Host = "http://localhost:8080",
            ApiKey = "qbt_abcdefghijklmnopqrstuvwxyz12",
            Password = "pass",
        };

        // Act / Assert
        Cleanuparr.Domain.Exceptions.CustomValidationException exception = await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.CustomValidationException>(
            () => _controller.CreateDownloadClientConfig(request));
        exception.Message.ShouldBe("Use either an API key or a username and password, not both");
    }

    [Fact]
    public async Task CreateDownloadClientConfig_QBittorrentWithApiKeyAndHttpAuthUsername_PropagatesValidationException()
    {
        // Arrange
        var request = new CreateDownloadClientRequest
        {
            Enabled = true,
            Name = "qbit-httpauth-conflict-client",
            TypeName = DownloadClientTypeName.qBittorrent,
            Host = "http://localhost:8080",
            ApiKey = "qbt_abcdefghijklmnopqrstuvwxyz12",
            HttpAuthUsername = "proxyuser",
        };

        // Act / Assert
        Cleanuparr.Domain.Exceptions.CustomValidationException exception = await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.CustomValidationException>(
            () => _controller.CreateDownloadClientConfig(request));
        exception.Message.ShouldBe("API key can't be combined with HTTP authentication. Use qBittorrent username/password instead.");
    }

    [Fact]
    public async Task CreateDownloadClientConfig_DigestWithoutUsername_PropagatesValidationException()
    {
        // Arrange
        var request = new CreateDownloadClientRequest
        {
            Enabled = true,
            Name = "rtorrent-digest-no-creds",
            TypeName = DownloadClientTypeName.rTorrent,
            Host = "http://localhost:8080",
            HttpAuthType = HttpAuthType.Digest,
        };

        // Act / Assert
        Cleanuparr.Domain.Exceptions.CustomValidationException exception = await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.CustomValidationException>(
            () => _controller.CreateDownloadClientConfig(request));
        exception.Message.ShouldBe("HTTP Digest authentication requires a username");
    }

    [Fact]
    public async Task CreateDownloadClientConfig_DigestWithUsername_PersistsHttpAuthType()
    {
        // Arrange
        var request = new CreateDownloadClientRequest
        {
            Enabled = true,
            Name = "rtorrent-digest-with-creds",
            TypeName = DownloadClientTypeName.rTorrent,
            Host = "http://localhost:8080",
            HttpAuthType = HttpAuthType.Digest,
            Username = "proxyuser",
            Password = "proxypass",
        };

        // Act
        await _controller.CreateDownloadClientConfig(request);

        // Assert
        var saved = await _dataContext.DownloadClients.AsNoTracking().FirstAsync(c => c.Name == "rtorrent-digest-with-creds");
        saved.HttpAuthType.ShouldBe(HttpAuthType.Digest);
    }

    [Fact]
    public async Task CreateDownloadClientConfig_PlaceholderHttpAuthPassword_PropagatesValidationException()
    {
        // Arrange
        CreateDownloadClientRequest request = new()
        {
            Enabled = true,
            Name = "qbit-httpauth-placeholder",
            TypeName = DownloadClientTypeName.qBittorrent,
            Host = "http://localhost:8080",
            HttpAuthUsername = "proxyuser",
            HttpAuthPassword = SensitiveDataHelper.Placeholder,
        };

        // Act / Assert
        Cleanuparr.Domain.Exceptions.CustomValidationException exception = await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.CustomValidationException>(
            () => _controller.CreateDownloadClientConfig(request));
        exception.Message.ShouldBe("HTTP auth password cannot be a placeholder value");
    }

    [Fact]
    public async Task CreateDownloadClientConfig_SabnzbdWithNoType_StoresUsenet()
    {
        // Arrange
        var request = new CreateDownloadClientRequest
        {
            Enabled = true,
            Name = "sab-client",
            TypeName = DownloadClientTypeName.Sabnzbd,
            Host = "http://localhost:8080",
            ApiKey = "key",
        };

        // Act
        await _controller.CreateDownloadClientConfig(request);

        // Assert
        var saved = await _dataContext.DownloadClients.AsNoTracking().FirstAsync(c => c.Name == "sab-client");
        saved.Type.ShouldBe(DownloadClientType.Usenet);
    }

    [Fact]
    public async Task CreateDownloadClientConfig_SabnzbdWithStaleTorrentTypeInBody_StoresUsenet()
    {
        // Arrange: CreateDownloadClientRequest no longer declares Type; STJ ignores
        // the stale "type" field an older frontend build might still send
        var json = """
        {
            "enabled": true,
            "name": "sab-client-2",
            "typeName": "Sabnzbd",
            "type": "Torrent",
            "host": "http://localhost:8080",
            "apiKey": "key"
        }
        """;
        var jsonOptions = new System.Text.Json.JsonSerializerOptions();
        Cleanuparr.Api.Json.CleanuparrJsonConfiguration.ConfigureApiInbound(jsonOptions);
        var request = System.Text.Json.JsonSerializer.Deserialize<CreateDownloadClientRequest>(json, jsonOptions)!;

        // Act
        await _controller.CreateDownloadClientConfig(request);

        // Assert
        var saved = await _dataContext.DownloadClients.AsNoTracking().FirstAsync(c => c.Name == "sab-client-2");
        saved.Type.ShouldBe(DownloadClientType.Usenet);
    }

    [Fact]
    public async Task CreateDownloadClientConfig_InvalidHost_PropagatesValidationException()
    {
        // Arrange
        var request = new CreateDownloadClientRequest
        {
            Name = "x",
            Host = string.Empty,
            TypeName = DownloadClientTypeName.qBittorrent,
        };

        // Act / Assert — Validate throws, controller's generic catch logs and re-throws
        await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.CustomValidationException>(
            () => _controller.CreateDownloadClientConfig(request));
    }

    [Fact]
    public async Task CreateDownloadClientConfig_UnparseableHost_PropagatesValidationException()
    {
        // Arrange
        var request = new CreateDownloadClientRequest
        {
            Name = "x",
            Host = "http://host:abc",
            TypeName = DownloadClientTypeName.qBittorrent,
        };

        // Act / Assert
        Cleanuparr.Domain.Exceptions.CustomValidationException exception = await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.CustomValidationException>(
            () => _controller.CreateDownloadClientConfig(request));
        exception.Message.ShouldBe("Host is not a valid URL");
    }

    [Fact]
    public async Task CreateDownloadClientConfig_HostWithUserInfo_PropagatesValidationException()
    {
        // Arrange
        var request = new CreateDownloadClientRequest
        {
            Name = "x",
            Host = "http://user:pass@localhost:8080",
            TypeName = DownloadClientTypeName.qBittorrent,
        };

        // Act / Assert
        Cleanuparr.Domain.Exceptions.CustomValidationException exception = await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.CustomValidationException>(
            () => _controller.CreateDownloadClientConfig(request));
        exception.Message.ShouldBe("Credentials in the URL are not supported. Use the HTTP authentication fields instead.");
    }

    [Fact]
    public async Task CreateDownloadClientConfig_RelativeHost_ReturnsCreated()
    {
        // Arrange
        var request = new CreateDownloadClientRequest
        {
            Name = "x",
            Host = "192.168.1.5",
            TypeName = DownloadClientTypeName.qBittorrent,
        };

        // Act
        IActionResult result = await _controller.CreateDownloadClientConfig(request);

        // Assert
        result.ShouldBeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task CreateDownloadClientConfig_UnknownTypeName_PropagatesValidationException()
    {
        // Arrange
        CreateDownloadClientRequest request = new()
        {
            Name = "x",
            Host = "http://localhost:8080",
            TypeName = DownloadClientTypeName.Unknown,
        };

        // Act / Assert
        await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.CustomValidationException>(
            () => _controller.CreateDownloadClientConfig(request));
    }

    [Fact]
    public async Task CreateDownloadClientConfig_UndefinedTypeName_PropagatesValidationException()
    {
        // Arrange
        CreateDownloadClientRequest request = new()
        {
            Name = "x",
            Host = "http://localhost:8080",
            TypeName = (DownloadClientTypeName)42,
        };

        // Act / Assert
        await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.CustomValidationException>(
            () => _controller.CreateDownloadClientConfig(request));
    }

    [Fact]
    public async Task UpdateDownloadClientConfig_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateDownloadClientRequest
        {
            Name = "x",
            Host = "http://localhost:8080",
            TypeName = DownloadClientTypeName.qBittorrent,
        };

        // Act
        var result = await _controller.UpdateDownloadClientConfig(Guid.NewGuid(), request);

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task UpdateDownloadClientConfig_ExistingClient_PersistsChanges()
    {
        // Arrange
        var client = NewClient("orig", DownloadClientTypeName.qBittorrent);
        _dataContext.DownloadClients.Add(client);
        await _dataContext.SaveChangesAsync();

        var request = new UpdateDownloadClientRequest
        {
            Enabled = true,
            Name = "renamed",
            Host = "http://newhost:9090",
            TypeName = DownloadClientTypeName.qBittorrent,
        };

        // Act
        var result = await _controller.UpdateDownloadClientConfig(client.Id, request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        var saved = await _dataContext.DownloadClients.AsNoTracking().FirstAsync(c => c.Id == client.Id);
        saved.Name.ShouldBe("renamed");
        saved.Host!.ToString().ShouldContain("newhost");
        _dynamicHttpClientFactory.Received(1).UnregisterConfiguration($"DownloadClient_{client.Id}");
    }

    [Fact]
    public async Task UpdateDownloadClientConfig_SwitchFromCredentialsToApiKey_ClearingUsernameAndPassword_Succeeds()
    {
        // Arrange
        DownloadClientConfig client = new()
        {
            Id = Guid.NewGuid(),
            Name = "qbit-client",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            Username = "admin",
            Password = "admin",
        };
        _dataContext.DownloadClients.Add(client);
        await _dataContext.SaveChangesAsync();

        UpdateDownloadClientRequest request = new()
        {
            Name = "qbit-client",
            Host = "http://localhost:8080",
            TypeName = DownloadClientTypeName.qBittorrent,
            Username = null,
            Password = null,
            ApiKey = "qbt_abcdefghijklmnopqrstuvwxyz12",
        };

        // Act
        var result = await _controller.UpdateDownloadClientConfig(client.Id, request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        DownloadClientConfig saved = await _dataContext.DownloadClients.AsNoTracking().FirstAsync(c => c.Id == client.Id);
        saved.ApiKey.ShouldBe("qbt_abcdefghijklmnopqrstuvwxyz12");
        saved.Username.ShouldBeNull();
        saved.Password.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateDownloadClientConfig_PlaceholderApiKeyOnClientWithNoStoredKey_PropagatesValidationException()
    {
        // Arrange
        DownloadClientConfig client = NewClient("orig", DownloadClientTypeName.qBittorrent);
        _dataContext.DownloadClients.Add(client);
        await _dataContext.SaveChangesAsync();

        UpdateDownloadClientRequest request = new()
        {
            Name = "renamed",
            Host = "http://newhost:9090",
            TypeName = DownloadClientTypeName.Sabnzbd,
            ApiKey = SensitiveDataHelper.Placeholder,
        };

        // Act / Assert
        await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.CustomValidationException>(
            () => _controller.UpdateDownloadClientConfig(client.Id, request));
    }

    [Fact]
    public async Task UpdateDownloadClientConfig_PlaceholderHttpAuthPassword_KeepsExistingHttpAuthPassword()
    {
        // Arrange
        DownloadClientConfig client = new()
        {
            Id = Guid.NewGuid(),
            Name = "qbit-client",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            HttpAuthUsername = "proxyuser",
            HttpAuthPassword = "stored-http-pass",
        };
        _dataContext.DownloadClients.Add(client);
        await _dataContext.SaveChangesAsync();

        UpdateDownloadClientRequest request = new()
        {
            Name = "qbit-client",
            Host = "http://localhost:8080",
            TypeName = DownloadClientTypeName.qBittorrent,
            HttpAuthUsername = "proxyuser",
            HttpAuthPassword = SensitiveDataHelper.Placeholder,
        };

        // Act
        IActionResult result = await _controller.UpdateDownloadClientConfig(client.Id, request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        DownloadClientConfig saved = await _dataContext.DownloadClients.AsNoTracking().FirstAsync(c => c.Id == client.Id);
        saved.HttpAuthPassword.ShouldBe("stored-http-pass");
    }

    [Fact]
    public async Task UpdateDownloadClientConfig_UnparseableHost_PropagatesValidationException()
    {
        // Arrange
        DownloadClientConfig client = NewClient("orig", DownloadClientTypeName.qBittorrent);
        _dataContext.DownloadClients.Add(client);
        await _dataContext.SaveChangesAsync();

        UpdateDownloadClientRequest request = new()
        {
            Name = "renamed",
            Host = "http://host:abc",
            TypeName = DownloadClientTypeName.qBittorrent,
        };

        // Act / Assert
        Cleanuparr.Domain.Exceptions.CustomValidationException exception = await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.CustomValidationException>(
            () => _controller.UpdateDownloadClientConfig(client.Id, request));
        exception.Message.ShouldBe("Host is not a valid URL");
    }

    [Fact]
    public async Task UpdateDownloadClientConfig_HostWithUserInfo_PropagatesValidationException()
    {
        // Arrange
        DownloadClientConfig client = NewClient("orig", DownloadClientTypeName.qBittorrent);
        _dataContext.DownloadClients.Add(client);
        await _dataContext.SaveChangesAsync();

        UpdateDownloadClientRequest request = new()
        {
            Name = "renamed",
            Host = "http://user:pass@newhost:9090",
            TypeName = DownloadClientTypeName.qBittorrent,
        };

        // Act / Assert
        Cleanuparr.Domain.Exceptions.CustomValidationException exception = await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.CustomValidationException>(
            () => _controller.UpdateDownloadClientConfig(client.Id, request));
        exception.Message.ShouldBe("Credentials in the URL are not supported. Use the HTTP authentication fields instead.");
    }

    [Fact]
    public async Task UpdateDownloadClientConfig_UnknownTypeName_PropagatesValidationException()
    {
        // Arrange
        DownloadClientConfig client = NewClient("orig", DownloadClientTypeName.qBittorrent);
        _dataContext.DownloadClients.Add(client);
        await _dataContext.SaveChangesAsync();

        UpdateDownloadClientRequest request = new()
        {
            Name = "renamed",
            Host = "http://newhost:9090",
            TypeName = DownloadClientTypeName.Unknown,
        };

        // Act / Assert
        await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.CustomValidationException>(
            () => _controller.UpdateDownloadClientConfig(client.Id, request));
    }

    [Fact]
    public async Task UpdateDownloadClientConfig_UndefinedTypeName_PropagatesValidationException()
    {
        // Arrange
        DownloadClientConfig client = NewClient("orig", DownloadClientTypeName.qBittorrent);
        _dataContext.DownloadClients.Add(client);
        await _dataContext.SaveChangesAsync();

        UpdateDownloadClientRequest request = new()
        {
            Name = "renamed",
            Host = "http://newhost:9090",
            TypeName = (DownloadClientTypeName)42,
        };

        // Act / Assert
        await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.CustomValidationException>(
            () => _controller.UpdateDownloadClientConfig(client.Id, request));
    }

    [Fact]
    public async Task UpdateDownloadClientConfig_ChangedTypeName_PropagatesValidationException()
    {
        // Arrange
        DownloadClientConfig client = NewClient("orig", DownloadClientTypeName.qBittorrent);
        _dataContext.DownloadClients.Add(client);
        await _dataContext.SaveChangesAsync();

        UpdateDownloadClientRequest request = new()
        {
            Name = "renamed",
            Host = "http://newhost:9090",
            TypeName = DownloadClientTypeName.rTorrent,
        };

        // Act / Assert
        Cleanuparr.Domain.Exceptions.CustomValidationException exception = await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.CustomValidationException>(
            () => _controller.UpdateDownloadClientConfig(client.Id, request));
        exception.Message.ShouldContain("cannot be changed");

        DownloadClientConfig saved = await _dataContext.DownloadClients.AsNoTracking().FirstAsync(c => c.Id == client.Id);
        saved.TypeName.ShouldBe(DownloadClientTypeName.qBittorrent);
        _dynamicHttpClientFactory.DidNotReceive().UnregisterConfiguration(Arg.Any<string>());
    }

    [Fact]
    public async Task DeleteDownloadClientConfig_ExistingClient_RemovesAndUnregistersHttpClient()
    {
        // Arrange
        var client = NewClient("doomed", DownloadClientTypeName.qBittorrent);
        _dataContext.DownloadClients.Add(client);
        await _dataContext.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteDownloadClientConfig(client.Id);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        (await _dataContext.DownloadClients.CountAsync()).ShouldBe(0);
        _dynamicHttpClientFactory.Received(1).UnregisterConfiguration($"DownloadClient_{client.Id}");
    }

    [Fact]
    public async Task DeleteDownloadClientConfig_UnknownId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.DeleteDownloadClientConfig(Guid.NewGuid());

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(404);
        _dynamicHttpClientFactory.DidNotReceive().UnregisterConfiguration(Arg.Any<string>());
    }

    [Fact]
    public async Task TestDownloadClient_Healthy_ReturnsOkWithResponseTime()
    {
        // Arrange
        var downloadService = Substitute.For<IDownloadService>();
        downloadService.HealthCheckAsync().Returns(new HealthCheckResult
        {
            IsHealthy = true,
            ResponseTime = TimeSpan.FromMilliseconds(123),
        });
        _downloadServiceFactory.GetDownloadService(Arg.Any<DownloadClientConfig>()).Returns(downloadService);

        var request = new TestDownloadClientRequest
        {
            TypeName = DownloadClientTypeName.qBittorrent,
            Host = "http://localhost:8080",
            Password = "pass",
        };

        // Act
        var result = await _controller.TestDownloadClient(request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task TestDownloadClient_Unhealthy_ReturnsBadRequestWithMessage()
    {
        // Arrange
        var downloadService = Substitute.For<IDownloadService>();
        downloadService.HealthCheckAsync().Returns(new HealthCheckResult
        {
            IsHealthy = false,
            ErrorMessage = "connection refused",
        });
        _downloadServiceFactory.GetDownloadService(Arg.Any<DownloadClientConfig>()).Returns(downloadService);

        var request = new TestDownloadClientRequest
        {
            TypeName = DownloadClientTypeName.qBittorrent,
            Host = "http://localhost:8080",
            Password = "pass",
        };

        // Act
        var result = await _controller.TestDownloadClient(request);

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task TestDownloadClient_QBittorrentWithApiKeyAndPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new TestDownloadClientRequest
        {
            TypeName = DownloadClientTypeName.qBittorrent,
            Host = "http://localhost:8080",
            ApiKey = "qbt_abcdefghijklmnopqrstuvwxyz12",
            Password = "pass",
        };

        // Act
        var result = await _controller.TestDownloadClient(request);

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(400);
        _downloadServiceFactory.DidNotReceive().GetDownloadService(Arg.Any<DownloadClientConfig>());
    }

    [Fact]
    public async Task TestDownloadClient_QBittorrentWithApiKeyAndHttpAuthUsername_ReturnsBadRequest()
    {
        // Arrange
        var request = new TestDownloadClientRequest
        {
            TypeName = DownloadClientTypeName.qBittorrent,
            Host = "http://localhost:8080",
            ApiKey = "qbt_abcdefghijklmnopqrstuvwxyz12",
            HttpAuthUsername = "proxyuser",
        };

        // Act
        var result = await _controller.TestDownloadClient(request);

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(400);
        _downloadServiceFactory.DidNotReceive().GetDownloadService(Arg.Any<DownloadClientConfig>());
    }

    [Fact]
    public async Task TestDownloadClient_DigestWithoutUsername_ReturnsBadRequest()
    {
        // Arrange
        var request = new TestDownloadClientRequest
        {
            TypeName = DownloadClientTypeName.rTorrent,
            Host = "http://localhost:8080",
            HttpAuthType = HttpAuthType.Digest,
        };

        // Act
        var result = await _controller.TestDownloadClient(request);

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(400);
        _downloadServiceFactory.DidNotReceive().GetDownloadService(Arg.Any<DownloadClientConfig>());
    }

    [Fact]
    public async Task TestDownloadClient_NewApiKeyWithPlaceholderPassword_UsesNewApiKeyAndStoredPassword()
    {
        // Arrange
        var client = new DownloadClientConfig
        {
            Name = "sab",
            TypeName = DownloadClientTypeName.Sabnzbd,
            Type = DownloadClientType.Usenet,
            Host = new Uri("http://localhost:8080"),
            Password = "stored-password",
            ApiKey = "stored-api-key",
        };
        _dataContext.DownloadClients.Add(client);
        await _dataContext.SaveChangesAsync();

        DownloadClientConfig? capturedConfig = null;
        var downloadService = Substitute.For<IDownloadService>();
        downloadService.HealthCheckAsync().Returns(new HealthCheckResult { IsHealthy = true });
        _downloadServiceFactory.GetDownloadService(Arg.Do<DownloadClientConfig>(c => capturedConfig = c)).Returns(downloadService);

        var request = new TestDownloadClientRequest
        {
            TypeName = DownloadClientTypeName.Sabnzbd,
            Host = "http://localhost:8080",
            Password = SensitiveDataHelper.Placeholder,
            ApiKey = "new-api-key",
            ClientId = client.Id,
        };

        // Act
        await _controller.TestDownloadClient(request);

        // Assert
        capturedConfig.ShouldNotBeNull();
        capturedConfig.ApiKey.ShouldBe("new-api-key");
        capturedConfig.Password.ShouldBe("stored-password");
    }

    [Fact]
    public async Task TestDownloadClient_NewPasswordWithPlaceholderApiKey_UsesNewPasswordAndStoredApiKey()
    {
        // Arrange
        var client = new DownloadClientConfig
        {
            Name = "sab",
            TypeName = DownloadClientTypeName.Sabnzbd,
            Type = DownloadClientType.Usenet,
            Host = new Uri("http://localhost:8080"),
            Password = "stored-password",
            ApiKey = "stored-api-key",
        };
        _dataContext.DownloadClients.Add(client);
        await _dataContext.SaveChangesAsync();

        DownloadClientConfig? capturedConfig = null;
        var downloadService = Substitute.For<IDownloadService>();
        downloadService.HealthCheckAsync().Returns(new HealthCheckResult { IsHealthy = true });
        _downloadServiceFactory.GetDownloadService(Arg.Do<DownloadClientConfig>(c => capturedConfig = c)).Returns(downloadService);

        var request = new TestDownloadClientRequest
        {
            TypeName = DownloadClientTypeName.Sabnzbd,
            Host = "http://localhost:8080",
            Password = "new-password",
            ApiKey = SensitiveDataHelper.Placeholder,
            ClientId = client.Id,
        };

        // Act
        await _controller.TestDownloadClient(request);

        // Assert
        capturedConfig.ShouldNotBeNull();
        capturedConfig.Password.ShouldBe("new-password");
        capturedConfig.ApiKey.ShouldBe("stored-api-key");
    }

    [Fact]
    public async Task TestDownloadClient_NewPasswordWithPlaceholderHttpAuthPassword_UsesStoredHttpAuthPassword()
    {
        // Arrange
        DownloadClientConfig client = new()
        {
            Name = "deluge",
            TypeName = DownloadClientTypeName.Deluge,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            Password = "stored-password",
            HttpAuthUsername = "proxyuser",
            HttpAuthPassword = "stored-http-pass",
        };
        _dataContext.DownloadClients.Add(client);
        await _dataContext.SaveChangesAsync();

        DownloadClientConfig? capturedConfig = null;
        IDownloadService downloadService = Substitute.For<IDownloadService>();
        downloadService.HealthCheckAsync().Returns(new HealthCheckResult { IsHealthy = true });
        _downloadServiceFactory.GetDownloadService(Arg.Do<DownloadClientConfig>(c => capturedConfig = c)).Returns(downloadService);

        TestDownloadClientRequest request = new()
        {
            TypeName = DownloadClientTypeName.Deluge,
            Host = "http://localhost:8080",
            Password = "new-password",
            HttpAuthUsername = "proxyuser",
            HttpAuthPassword = SensitiveDataHelper.Placeholder,
            ClientId = client.Id,
        };

        // Act
        await _controller.TestDownloadClient(request);

        // Assert
        capturedConfig.ShouldNotBeNull();
        capturedConfig.Password.ShouldBe("new-password");
        capturedConfig.HttpAuthPassword.ShouldBe("stored-http-pass");
    }

    [Fact]
    public async Task TestDownloadClient_RawPlaceholderHttpAuthPasswordNoClientId_ReturnsBadRequest()
    {
        // Arrange: no ClientId, so there is no stored value to resolve the placeholder against
        TestDownloadClientRequest request = new()
        {
            TypeName = DownloadClientTypeName.Deluge,
            Host = "http://localhost:8080",
            HttpAuthUsername = "proxyuser",
            HttpAuthPassword = SensitiveDataHelper.Placeholder,
        };

        // Act
        IActionResult result = await _controller.TestDownloadClient(request);

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(400);
        _downloadServiceFactory.DidNotReceive().GetDownloadService(Arg.Any<DownloadClientConfig>());
    }

    [Fact]
    public async Task TestDownloadClient_InvalidHost_ReturnsBadRequest()
    {
        // Arrange — empty host fails Validate; the controller wraps the exception in BadRequest
        var request = new TestDownloadClientRequest
        {
            TypeName = DownloadClientTypeName.qBittorrent,
            Host = string.Empty,
            Password = "pass",
        };

        // Act
        var result = await _controller.TestDownloadClient(request);

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task TestDownloadClient_UnparseableHost_ReturnsBadRequest()
    {
        // Arrange
        TestDownloadClientRequest request = new()
        {
            TypeName = DownloadClientTypeName.qBittorrent,
            Host = "http://host:abc",
            Password = "pass",
        };

        // Act
        IActionResult result = await _controller.TestDownloadClient(request);

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(400);
        _downloadServiceFactory.DidNotReceive().GetDownloadService(Arg.Any<DownloadClientConfig>());
    }

    [Fact]
    public async Task TestDownloadClient_HostWithUserInfo_ReturnsBadRequest()
    {
        // Arrange
        TestDownloadClientRequest request = new()
        {
            TypeName = DownloadClientTypeName.qBittorrent,
            Host = "http://user:pass@localhost:8080",
            Password = "pass",
        };

        // Act
        IActionResult result = await _controller.TestDownloadClient(request);

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(400);
        _downloadServiceFactory.DidNotReceive().GetDownloadService(Arg.Any<DownloadClientConfig>());
    }

    [Fact]
    public async Task TestDownloadClient_UnknownTypeName_ReturnsBadRequest()
    {
        // Arrange
        TestDownloadClientRequest request = new()
        {
            TypeName = DownloadClientTypeName.Unknown,
            Host = "http://localhost:8080",
            Password = "pass",
        };

        // Act
        IActionResult result = await _controller.TestDownloadClient(request);

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task TestDownloadClient_UndefinedTypeName_ReturnsBadRequest()
    {
        // Arrange
        TestDownloadClientRequest request = new()
        {
            TypeName = (DownloadClientTypeName)42,
            Host = "http://localhost:8080",
            Password = "pass",
        };

        // Act
        IActionResult result = await _controller.TestDownloadClient(request);

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(400);
    }

    private static DownloadClientConfig NewClient(string name, DownloadClientTypeName typeName) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        TypeName = typeName,
        Type = typeName.ClientType(),
        Host = new Uri("http://localhost:8080"),
    };
}

using Cleanuparr.Api.Features.DownloadClient.Contracts.Requests;
using Cleanuparr.Api.Features.DownloadClient.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Entities.HealthCheck;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
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
        var dict = ok.Value!.GetType().GetProperty("clients")!.GetValue(ok.Value) as List<DownloadClientConfig>;
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
            Type = DownloadClientType.Torrent,
            Host = "http://localhost:8080",
            Username = "user",
            Password = "pass",
        };

        // Act
        var result = await _controller.CreateDownloadClientConfig(request);

        // Assert
        var created = result.ShouldBeOfType<CreatedAtActionResult>();
        var entity = created.Value.ShouldBeOfType<DownloadClientConfig>();
        entity.Name.ShouldBe("my-client");
        (await _dataContext.DownloadClients.CountAsync()).ShouldBe(1);
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
            Type = DownloadClientType.Torrent,
        };

        // Act / Assert — Validate throws, controller's generic catch logs and re-throws
        await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.ValidationException>(
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
            Type = DownloadClientType.Torrent,
        };

        // Act
        var result = await _controller.UpdateDownloadClientConfig(Guid.NewGuid(), request);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
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
            Type = DownloadClientType.Torrent,
        };

        // Act
        var result = await _controller.UpdateDownloadClientConfig(client.Id, request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        var saved = await _dataContext.DownloadClients.AsNoTracking().FirstAsync(c => c.Id == client.Id);
        saved.Name.ShouldBe("renamed");
        saved.Host!.ToString().ShouldContain("newhost");
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
        result.ShouldBeOfType<NotFoundObjectResult>();
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
            Type = DownloadClientType.Torrent,
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
            Type = DownloadClientType.Torrent,
            Host = "http://localhost:8080",
            Password = "pass",
        };

        // Act
        var result = await _controller.TestDownloadClient(request);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TestDownloadClient_InvalidHost_ReturnsBadRequest()
    {
        // Arrange — empty host fails Validate; the controller wraps the exception in BadRequest
        var request = new TestDownloadClientRequest
        {
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = string.Empty,
            Password = "pass",
        };

        // Act
        var result = await _controller.TestDownloadClient(request);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    private static DownloadClientConfig NewClient(string name, DownloadClientTypeName typeName) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        TypeName = typeName,
        Type = DownloadClientType.Torrent,
        Host = new Uri("http://localhost:8080"),
    };
}

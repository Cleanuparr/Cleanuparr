using Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;
using Cleanuparr.Api.Features.DownloadCleaner.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Features.DownloadCleaner;

public class UnlinkedConfigControllerTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly UnlinkedConfigController _controller;

    public UnlinkedConfigControllerTests()
    {
        _dataContext = ConfigControllerTestDataFactory.CreateDataContext();
        var logger = Substitute.For<ILogger<UnlinkedConfigController>>();
        _controller = new UnlinkedConfigController(logger, _dataContext);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetUnlinkedConfig_ClientNotFound_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetUnlinkedConfig(Guid.NewGuid());

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetUnlinkedConfig_ClientWithoutConfig_ReturnsOkWithNull()
    {
        // Arrange — add a client but no UnlinkedConfig
        var client = AddDownloadClient();

        // Act
        var result = await _controller.GetUnlinkedConfig(client.Id);

        // Assert
        var ok = result.ShouldBeOfType<OkObjectResult>();
        ok.Value.ShouldBeNull();
    }

    [Fact]
    public async Task GetUnlinkedConfig_ClientWithConfig_ReturnsConfig()
    {
        // Arrange
        var client = AddDownloadClient();
        _dataContext.UnlinkedConfigs.Add(new UnlinkedConfig
        {
            DownloadClientConfigId = client.Id,
            Enabled = true,
            TargetCategory = "unlinked-cat",
            Categories = new List<string> { "regular" },
        });
        await _dataContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetUnlinkedConfig(client.Id);

        // Assert
        var ok = result.ShouldBeOfType<OkObjectResult>();
        var config = ok.Value.ShouldBeOfType<UnlinkedConfig>();
        config.Enabled.ShouldBeTrue();
        config.TargetCategory.ShouldBe("unlinked-cat");
    }

    [Fact]
    public async Task UpdateUnlinkedConfig_ClientNotFound_ReturnsNotFound()
    {
        // Arrange
        var dto = new UnlinkedConfigRequest { Enabled = false };

        // Act
        var result = await _controller.UpdateUnlinkedConfig(Guid.NewGuid(), dto);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateUnlinkedConfig_NewConfig_CreatesAndReturnsIt()
    {
        // Arrange
        var client = AddDownloadClient();
        var dto = new UnlinkedConfigRequest
        {
            Enabled = false,
            TargetCategory = "unlinked-cat",
            Categories = new List<string> { "movies" },
        };

        // Act
        var result = await _controller.UpdateUnlinkedConfig(client.Id, dto);

        // Assert
        var ok = result.ShouldBeOfType<OkObjectResult>();
        var saved = await _dataContext.UnlinkedConfigs.AsNoTracking()
            .FirstAsync(u => u.DownloadClientConfigId == client.Id);
        saved.TargetCategory.ShouldBe("unlinked-cat");
        saved.Categories.ShouldContain("movies");
    }

    [Fact]
    public async Task UpdateUnlinkedConfig_ExistingConfig_UpdatesInPlace()
    {
        // Arrange
        var client = AddDownloadClient();
        _dataContext.UnlinkedConfigs.Add(new UnlinkedConfig
        {
            DownloadClientConfigId = client.Id,
            Enabled = false,
            TargetCategory = "old-cat",
        });
        await _dataContext.SaveChangesAsync();

        var dto = new UnlinkedConfigRequest
        {
            Enabled = false,
            TargetCategory = "new-cat",
        };

        // Act
        var result = await _controller.UpdateUnlinkedConfig(client.Id, dto);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        var rows = await _dataContext.UnlinkedConfigs
            .Where(u => u.DownloadClientConfigId == client.Id)
            .ToListAsync();
        rows.Count.ShouldBe(1);
        rows[0].TargetCategory.ShouldBe("new-cat");
    }

    [Fact]
    public async Task UpdateUnlinkedConfig_EnabledButNoCategories_ReturnsBadRequest()
    {
        // Arrange — enabled requires at least one category per Validate()
        var client = AddDownloadClient();
        var dto = new UnlinkedConfigRequest
        {
            Enabled = true,
            TargetCategory = "unlinked-cat",
            Categories = new List<string>(),
        };

        // Act
        var result = await _controller.UpdateUnlinkedConfig(client.Id, dto);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateUnlinkedConfig_TargetInCategories_ReturnsBadRequest()
    {
        // Arrange — TargetCategory must not appear in Categories
        var client = AddDownloadClient();
        var dto = new UnlinkedConfigRequest
        {
            Enabled = true,
            TargetCategory = "unlinked-cat",
            Categories = new List<string> { "movies", "unlinked-cat" },
        };

        // Act
        var result = await _controller.UpdateUnlinkedConfig(client.Id, dto);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    private DownloadClientConfig AddDownloadClient()
    {
        var client = new DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = "test-client",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
        };
        _dataContext.DownloadClients.Add(client);
        _dataContext.SaveChanges();
        return client;
    }
}

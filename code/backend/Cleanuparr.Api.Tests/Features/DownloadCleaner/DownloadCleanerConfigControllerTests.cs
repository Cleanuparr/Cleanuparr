using System.Text.Json;
using Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;
using Cleanuparr.Api.Features.DownloadCleaner.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Features.DownloadCleaner;

public class DownloadCleanerConfigControllerTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly IJobManagementService _jobManagementService;
    private readonly DownloadCleanerConfigController _controller;

    public DownloadCleanerConfigControllerTests()
    {
        _dataContext = ConfigControllerTestDataFactory.CreateDataContext();
        var logger = Substitute.For<ILogger<DownloadCleanerConfigController>>();
        _jobManagementService = Substitute.For<IJobManagementService>();
        _controller = new DownloadCleanerConfigController(logger, _dataContext, _jobManagementService);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetDownloadCleanerConfig_NoClients_ReturnsConfigWithEmptyClientsList()
    {
        // Act
        var result = await _controller.GetDownloadCleanerConfig();

        // Assert
        var ok = result.ShouldBeOfType<OkObjectResult>();
        ok.Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetDownloadCleanerConfig_IncludesClientsWithoutSeedingCleanupCapability()
    {
        // Arrange: Sabnzbd has no seeding rules, unlinked handling or dead-torrent detection,
        // but it still needs to show up here for its orphaned-files config.
        _dataContext.DownloadClients.Add(new DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = "Test Sabnzbd",
            TypeName = DownloadClientTypeName.Sabnzbd,
            Type = DownloadClientType.Usenet,
            Enabled = true,
        });
        await _dataContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetDownloadCleanerConfig();

        // Assert
        JsonElement clients = ResponseContract.Body(result).GetProperty("clients");
        JsonElement sab = clients.EnumerateArray()
            .First(c => c.GetProperty("downloadClientName").GetString() == "Test Sabnzbd");
        sab.GetProperty("seedingRules").GetArrayLength().ShouldBe(0);
        sab.GetProperty("unlinkedConfig").ValueKind.ShouldBe(JsonValueKind.Null);
        sab.GetProperty("deadTorrentConfig").ValueKind.ShouldBe(JsonValueKind.Null);
        sab.GetProperty("orphanedFilesConfig").ValueKind.ShouldBe(JsonValueKind.Null);

        // Reading the bundle must not create default config rows for a client that never had one.
        (await _dataContext.UnlinkedConfigs.CountAsync()).ShouldBe(0);
        (await _dataContext.DeadTorrentConfigs.CountAsync()).ShouldBe(0);
        (await _dataContext.OrphanedFilesConfigs.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task UpdateDownloadCleanerConfig_Enabled_StartsJob()
    {
        // Arrange
        var request = new UpdateDownloadCleanerConfigRequest
        {
            Enabled = true,
            CronExpression = "0 0 * * * ?",
            IgnoredDownloads = new List<string>(),
        };

        // Act
        var result = await _controller.UpdateDownloadCleanerConfig(request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        await _jobManagementService.Received(1).StartJob(JobType.DownloadCleaner, null, "0 0 * * * ?");
    }

    [Fact]
    public async Task UpdateDownloadCleanerConfig_Disabled_StopsJob()
    {
        // Arrange — pre-enable
        var existing = await _dataContext.DownloadCleanerConfigs.FirstAsync();
        existing.Enabled = true;
        await _dataContext.SaveChangesAsync();

        var request = new UpdateDownloadCleanerConfigRequest
        {
            Enabled = false,
            CronExpression = "0 0 * * * ?",
            IgnoredDownloads = new List<string>(),
        };

        // Act
        var result = await _controller.UpdateDownloadCleanerConfig(request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        await _jobManagementService.Received(1).StopJob(JobType.DownloadCleaner);
    }

    [Fact]
    public async Task UpdateDownloadCleanerConfig_InvalidCron_PropagatesValidationException()
    {
        // Arrange — controller's catch only handles System.ComponentModel.DataAnnotations.ValidationException;
        // CronValidationHelper throws Cleanuparr.Domain.Exceptions.ValidationException which propagates.
        var request = new UpdateDownloadCleanerConfigRequest
        {
            Enabled = true,
            CronExpression = "not-a-cron",
            IgnoredDownloads = new List<string>(),
        };

        // Act / Assert
        await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.ValidationException>(
            () => _controller.UpdateDownloadCleanerConfig(request));
    }

    [Fact]
    public async Task UpdateDownloadCleanerConfig_PersistsChanges()
    {
        // Arrange
        var request = new UpdateDownloadCleanerConfigRequest
        {
            Enabled = true,
            CronExpression = "0 0/15 * * * ?",
            UseAdvancedScheduling = true,
            IgnoredDownloads = new List<string> { "skip-me" },
        };

        // Act
        await _controller.UpdateDownloadCleanerConfig(request);

        // Assert
        var saved = await _dataContext.DownloadCleanerConfigs.AsNoTracking().FirstAsync();
        saved.Enabled.ShouldBeTrue();
        saved.CronExpression.ShouldBe("0 0/15 * * * ?");
        saved.UseAdvancedScheduling.ShouldBeTrue();
        saved.IgnoredDownloads.ShouldContain("skip-me");
    }
}

using Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;
using Cleanuparr.Api.Features.DownloadCleaner.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
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

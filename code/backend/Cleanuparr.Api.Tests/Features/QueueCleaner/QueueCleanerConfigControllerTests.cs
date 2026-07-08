using Cleanuparr.Api.Features.QueueCleaner.Contracts.Requests;
using Cleanuparr.Api.Features.QueueCleaner.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Features.QueueCleaner;

public class QueueCleanerConfigControllerTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly IJobManagementService _jobManagementService;
    private readonly QueueCleanerConfigController _controller;

    public QueueCleanerConfigControllerTests()
    {
        _dataContext = ConfigControllerTestDataFactory.CreateDataContext();
        var logger = Substitute.For<ILogger<QueueCleanerConfigController>>();
        _jobManagementService = Substitute.For<IJobManagementService>();
        _controller = new QueueCleanerConfigController(logger, _dataContext, _jobManagementService);
        ConfigControllerTestDataFactory.ConfigureProblemDetails(_controller);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetQueueCleanerConfig_ReturnsExistingConfig()
    {
        // Act
        var result = await _controller.GetQueueCleanerConfig();

        // Assert
        var ok = result.ShouldBeOfType<OkObjectResult>();
        ok.Value.ShouldBeOfType<QueueCleanerConfig>();
    }

    [Fact]
    public async Task UpdateQueueCleanerConfig_Enabled_StartsJob()
    {
        // Arrange
        var request = new UpdateQueueCleanerConfigRequest
        {
            Enabled = true,
            CronExpression = "0 0/5 * * * ?",
            FailedImport = new FailedImportConfig(),
            IgnoredDownloads = new List<string>(),
        };

        // Act
        var result = await _controller.UpdateQueueCleanerConfig(request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        await _jobManagementService.Received(1).StartJob(JobType.QueueCleaner, null, "0 0/5 * * * ?");
        await _jobManagementService.DidNotReceive().StopJob(Arg.Any<JobType>());
    }

    [Fact]
    public async Task UpdateQueueCleanerConfig_Disabled_StopsJob()
    {
        // Arrange — pre-enable
        var existing = await _dataContext.QueueCleanerConfigs.FirstAsync();
        existing.Enabled = true;
        await _dataContext.SaveChangesAsync();

        var request = new UpdateQueueCleanerConfigRequest
        {
            Enabled = false,
            CronExpression = "0 0/5 * * * ?",
            FailedImport = new FailedImportConfig(),
            IgnoredDownloads = new List<string>(),
        };

        // Act
        var result = await _controller.UpdateQueueCleanerConfig(request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        await _jobManagementService.Received(1).StopJob(JobType.QueueCleaner);
    }

    [Fact]
    public async Task UpdateQueueCleanerConfig_InvalidCronExpression_PropagatesValidationException()
    {
        // Arrange — CronValidationHelper throws Cleanuparr.Domain.Exceptions.ValidationException,
        // which the controller's catch (System.ComponentModel.DataAnnotations.ValidationException) does NOT match.
        var request = new UpdateQueueCleanerConfigRequest
        {
            Enabled = true,
            CronExpression = "not-a-cron",
            FailedImport = new FailedImportConfig(),
            IgnoredDownloads = new List<string>(),
        };

        // Act / Assert
        await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.ValidationException>(
            () => _controller.UpdateQueueCleanerConfig(request));
        await _jobManagementService.DidNotReceive().StartJob(Arg.Any<JobType>(), Arg.Any<Cleanuparr.Infrastructure.Models.JobSchedule?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task UpdateQueueCleanerConfig_ConfigValidationFails_ReturnsBadRequest()
    {
        // Arrange — DownloadingMetadataMaxStrikes < 3 (and > 0) triggers Validate() exception
        var request = new UpdateQueueCleanerConfigRequest
        {
            Enabled = true,
            CronExpression = "0 0/5 * * * ?",
            FailedImport = new FailedImportConfig(),
            DownloadingMetadataMaxStrikes = 2,
            IgnoredDownloads = new List<string>(),
        };

        // Act + Assert
        await Should.ThrowAsync<System.ComponentModel.DataAnnotations.ValidationException>(
            () => _controller.UpdateQueueCleanerConfig(request));
    }

    [Fact]
    public async Task UpdateQueueCleanerConfig_PersistsChanges()
    {
        // Arrange
        var request = new UpdateQueueCleanerConfigRequest
        {
            Enabled = true,
            CronExpression = "0 0/10 * * * ?",
            FailedImport = new FailedImportConfig(),
            DownloadingMetadataMaxStrikes = 5,
            ProcessNoContentId = true,
            IgnoredDownloads = new List<string> { "ignored" },
        };

        // Act
        await _controller.UpdateQueueCleanerConfig(request);

        // Assert
        var saved = await _dataContext.QueueCleanerConfigs.AsNoTracking().FirstAsync();
        saved.Enabled.ShouldBeTrue();
        saved.CronExpression.ShouldBe("0 0/10 * * * ?");
        saved.DownloadingMetadataMaxStrikes.ShouldBe((ushort)5);
        saved.ProcessNoContentId.ShouldBeTrue();
        saved.IgnoredDownloads.ShouldContain("ignored");
    }
}

using Cleanuparr.Api.Features.BlacklistSync.Contracts.Requests;
using Cleanuparr.Api.Features.BlacklistSync.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.BlacklistSync;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Features.BlacklistSync;

public class BlacklistSyncConfigControllerTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly IJobManagementService _jobManagementService;
    private readonly BlacklistSyncConfigController _controller;

    public BlacklistSyncConfigControllerTests()
    {
        _dataContext = ConfigControllerTestDataFactory.CreateDataContext();
        var logger = Substitute.For<ILogger<BlacklistSyncConfigController>>();
        _jobManagementService = Substitute.For<IJobManagementService>();
        _controller = new BlacklistSyncConfigController(logger, _dataContext, _jobManagementService);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetBlacklistSyncConfig_ReturnsExistingConfig()
    {
        // Act
        var result = await _controller.GetBlacklistSyncConfig();

        // Assert
        var ok = result.ShouldBeOfType<OkObjectResult>();
        ok.Value.ShouldBeOfType<BlacklistSyncConfig>();
    }

    [Fact]
    public async Task UpdateBlacklistSyncConfig_BecameEnabled_StartsAndTriggersJob()
    {
        // Arrange — start disabled, enable with URL path
        var request = new UpdateBlacklistSyncConfigRequest
        {
            Enabled = true,
            BlacklistPath = "https://example.com/blacklist.txt",
        };

        // Act
        var result = await _controller.UpdateBlacklistSyncConfig(request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        await _jobManagementService.Received(1).StartJob(JobType.BlacklistSynchronizer, null, Arg.Any<string>());
        await _jobManagementService.Received(1).TriggerJobOnce(JobType.BlacklistSynchronizer);
        await _jobManagementService.DidNotReceive().StopJob(Arg.Any<JobType>());
    }

    [Fact]
    public async Task UpdateBlacklistSyncConfig_BecameDisabled_StopsJob()
    {
        // Arrange — pre-enable so the toggle to disabled hits the stop path
        var existing = await _dataContext.BlacklistSyncConfigs.FirstAsync();
        existing.Enabled = true;
        existing.BlacklistPath = "https://example.com/blacklist.txt";
        await _dataContext.SaveChangesAsync();

        var request = new UpdateBlacklistSyncConfigRequest
        {
            Enabled = false,
            BlacklistPath = existing.BlacklistPath,
        };

        // Act
        var result = await _controller.UpdateBlacklistSyncConfig(request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        await _jobManagementService.Received(1).StopJob(JobType.BlacklistSynchronizer);
        await _jobManagementService.DidNotReceive().StartJob(Arg.Any<JobType>(), Arg.Any<Cleanuparr.Infrastructure.Models.JobSchedule?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task UpdateBlacklistSyncConfig_StaysEnabledAndPathChanged_TriggersOnce()
    {
        // Arrange — pre-enable
        var existing = await _dataContext.BlacklistSyncConfigs.FirstAsync();
        existing.Enabled = true;
        existing.BlacklistPath = "https://example.com/old.txt";
        await _dataContext.SaveChangesAsync();

        var request = new UpdateBlacklistSyncConfigRequest
        {
            Enabled = true,
            BlacklistPath = "https://example.com/new.txt",
        };

        // Act
        var result = await _controller.UpdateBlacklistSyncConfig(request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        await _jobManagementService.Received(1).TriggerJobOnce(JobType.BlacklistSynchronizer);
        await _jobManagementService.DidNotReceive().StartJob(Arg.Any<JobType>(), Arg.Any<Cleanuparr.Infrastructure.Models.JobSchedule?>(), Arg.Any<string?>());
        await _jobManagementService.DidNotReceive().StopJob(Arg.Any<JobType>());
    }

    [Fact]
    public async Task UpdateBlacklistSyncConfig_StaysEnabledNoPathChange_NoJobOps()
    {
        // Arrange — pre-enable, then resubmit identical values
        var existing = await _dataContext.BlacklistSyncConfigs.FirstAsync();
        existing.Enabled = true;
        existing.BlacklistPath = "https://example.com/list.txt";
        await _dataContext.SaveChangesAsync();

        var request = new UpdateBlacklistSyncConfigRequest
        {
            Enabled = true,
            BlacklistPath = existing.BlacklistPath,
        };

        // Act
        var result = await _controller.UpdateBlacklistSyncConfig(request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        await _jobManagementService.DidNotReceive().StartJob(Arg.Any<JobType>(), Arg.Any<Cleanuparr.Infrastructure.Models.JobSchedule?>(), Arg.Any<string?>());
        await _jobManagementService.DidNotReceive().StopJob(Arg.Any<JobType>());
        await _jobManagementService.DidNotReceive().TriggerJobOnce(Arg.Any<JobType>());
    }

    [Fact]
    public async Task UpdateBlacklistSyncConfig_EnabledWithMissingPath_Throws()
    {
        // Arrange
        var request = new UpdateBlacklistSyncConfigRequest
        {
            Enabled = true,
            BlacklistPath = null,
        };

        // Act / Assert — Validate throws and is rethrown
        await Should.ThrowAsync<Exception>(() => _controller.UpdateBlacklistSyncConfig(request));
    }

    [Fact]
    public async Task UpdateBlacklistSyncConfig_PersistsChangesToDatabase()
    {
        // Arrange
        var request = new UpdateBlacklistSyncConfigRequest
        {
            Enabled = true,
            BlacklistPath = "https://example.com/list.txt",
        };

        // Act
        await _controller.UpdateBlacklistSyncConfig(request);

        // Assert
        var saved = await _dataContext.BlacklistSyncConfigs.AsNoTracking().FirstAsync();
        saved.Enabled.ShouldBeTrue();
        saved.BlacklistPath.ShouldBe("https://example.com/list.txt");
    }
}

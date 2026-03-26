using Cleanuparr.Api.Features.Seeker.Contracts.Requests;
using Cleanuparr.Api.Features.Seeker.Contracts.Responses;
using Cleanuparr.Api.Features.Seeker.Controllers;
using Cleanuparr.Api.Tests.Features.Seeker.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Api.Tests.Features.Seeker;

public class SeekerConfigControllerTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly ILogger<SeekerConfigController> _logger;
    private readonly IJobManagementService _jobManagementService;
    private readonly SeekerConfigController _controller;

    public SeekerConfigControllerTests()
    {
        _dataContext = SeekerTestDataFactory.CreateDataContext();
        _logger = Substitute.For<ILogger<SeekerConfigController>>();
        _jobManagementService = Substitute.For<IJobManagementService>();
        _controller = new SeekerConfigController(_logger, _dataContext, _jobManagementService);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    #region GetSeekerConfig Tests

    [Fact]
    public async Task GetSeekerConfig_WithNoSeekerInstanceConfigs_ReturnsDefaults()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);

        var result = await _controller.GetSeekerConfig();
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var response = okResult.Value.ShouldBeOfType<SeekerConfigResponse>();

        var instance = response.Instances.ShouldHaveSingleItem();
        instance.ArrInstanceId.ShouldBe(radarr.Id);
        instance.Enabled.ShouldBeFalse();
        instance.SkipTags.ShouldBeEmpty();
        instance.ActiveDownloadLimit.ShouldBe(3);
        instance.MinCycleTimeDays.ShouldBe(7);
    }

    [Fact]
    public async Task GetSeekerConfig_OnlyReturnsSonarrAndRadarrInstances()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        var sonarr = SeekerTestDataFactory.AddSonarrInstance(_dataContext);
        var lidarr = SeekerTestDataFactory.AddLidarrInstance(_dataContext);

        var result = await _controller.GetSeekerConfig();
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var response = okResult.Value.ShouldBeOfType<SeekerConfigResponse>();

        response.Instances.Count.ShouldBe(2);
        response.Instances.ShouldContain(i => i.ArrInstanceId == radarr.Id);
        response.Instances.ShouldContain(i => i.ArrInstanceId == sonarr.Id);
        response.Instances.ShouldNotContain(i => i.ArrInstanceId == lidarr.Id);
    }

    #endregion

    #region UpdateSeekerConfig Tests

    [Fact]
    public async Task UpdateSeekerConfig_WithProactiveEnabledAndNoInstancesEnabled_ThrowsValidationException()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        var request = new UpdateSeekerConfigRequest
        {
            SearchEnabled = true,
            SearchInterval = 5,
            ProactiveSearchEnabled = true,
            Instances =
            [
                new UpdateSeekerInstanceConfigRequest
                {
                    ArrInstanceId = radarr.Id,
                    Enabled = false // No instances enabled
                }
            ]
        };

        await Should.ThrowAsync<ValidationException>(() => _controller.UpdateSeekerConfig(request));
    }

    [Fact]
    public async Task UpdateSeekerConfig_WhenIntervalChanges_ReschedulesSeeker()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarr.Id,
            Enabled = true
        });
        await _dataContext.SaveChangesAsync();

        // Default interval is 3, change to 5
        var request = new UpdateSeekerConfigRequest
        {
            SearchEnabled = true,
            SearchInterval = 5,
            ProactiveSearchEnabled = true,
            Instances =
            [
                new UpdateSeekerInstanceConfigRequest { ArrInstanceId = radarr.Id, Enabled = true }
            ]
        };

        await _controller.UpdateSeekerConfig(request);

        await _jobManagementService.Received(1)
            .StartJob(JobType.Seeker, null, Arg.Any<string>());
    }

    [Fact]
    public async Task UpdateSeekerConfig_WhenIntervalUnchanged_DoesNotReschedule()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarr.Id,
            Enabled = true
        });
        await _dataContext.SaveChangesAsync();

        // Keep interval at default (3)
        var request = new UpdateSeekerConfigRequest
        {
            SearchEnabled = true,
            SearchInterval = 3,
            ProactiveSearchEnabled = true,
            Instances =
            [
                new UpdateSeekerInstanceConfigRequest { ArrInstanceId = radarr.Id, Enabled = true }
            ]
        };

        await _controller.UpdateSeekerConfig(request);

        await _jobManagementService.DidNotReceive()
            .StartJob(Arg.Any<JobType>(), null, Arg.Any<string>());
    }

    [Fact]
    public async Task UpdateSeekerConfig_WhenCustomFormatScoreEnabled_StartsAndTriggersSyncerJob()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarr.Id,
            Enabled = true
        });
        await _dataContext.SaveChangesAsync();

        // UseCustomFormatScore was false (default), now enable it
        var request = new UpdateSeekerConfigRequest
        {
            SearchEnabled = true,
            SearchInterval = 3,
            ProactiveSearchEnabled = true,
            UseCustomFormatScore = true,
            Instances =
            [
                new UpdateSeekerInstanceConfigRequest { ArrInstanceId = radarr.Id, Enabled = true }
            ]
        };

        await _controller.UpdateSeekerConfig(request);

        await _jobManagementService.Received(1)
            .StartJob(JobType.CustomFormatScoreSyncer, null, Arg.Any<string>());
        await _jobManagementService.Received(1)
            .TriggerJobOnce(JobType.CustomFormatScoreSyncer);
    }

    [Fact]
    public async Task UpdateSeekerConfig_WhenCustomFormatScoreDisabled_StopsSyncerJob()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarr.Id,
            Enabled = true
        });
        await _dataContext.SaveChangesAsync();

        // First enable CF score
        var config = await _dataContext.SeekerConfigs.FirstAsync();
        config.UseCustomFormatScore = true;
        await _dataContext.SaveChangesAsync();

        // Now disable it
        var request = new UpdateSeekerConfigRequest
        {
            SearchEnabled = true,
            SearchInterval = 3,
            ProactiveSearchEnabled = true,
            UseCustomFormatScore = false,
            Instances =
            [
                new UpdateSeekerInstanceConfigRequest { ArrInstanceId = radarr.Id, Enabled = true }
            ]
        };

        await _controller.UpdateSeekerConfig(request);

        await _jobManagementService.Received(1)
            .StopJob(JobType.CustomFormatScoreSyncer);
    }

    [Fact]
    public async Task UpdateSeekerConfig_WhenSearchReenabledWithCustomFormatActive_TriggersSyncerOnce()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarr.Id,
            Enabled = true
        });
        await _dataContext.SaveChangesAsync();

        // Set up state: CF score already enabled, search currently disabled
        var config = await _dataContext.SeekerConfigs.FirstAsync();
        config.UseCustomFormatScore = true;
        config.SearchEnabled = false;
        await _dataContext.SaveChangesAsync();

        // Re-enable search
        var request = new UpdateSeekerConfigRequest
        {
            SearchEnabled = true,
            SearchInterval = 3,
            ProactiveSearchEnabled = false,
            UseCustomFormatScore = true,
            Instances =
            [
                new UpdateSeekerInstanceConfigRequest { ArrInstanceId = radarr.Id, Enabled = true }
            ]
        };

        await _controller.UpdateSeekerConfig(request);

        await _jobManagementService.Received(1)
            .TriggerJobOnce(JobType.CustomFormatScoreSyncer);
    }

    [Fact]
    public async Task UpdateSeekerConfig_SyncsExistingAndCreatesNewInstanceConfigs()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        var sonarr = SeekerTestDataFactory.AddSonarrInstance(_dataContext);

        // Radarr already has a config
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarr.Id,
            Enabled = false,
            SkipTags = ["old-tag"],
            ActiveDownloadLimit = 2,
            MinCycleTimeDays = 5
        });
        await _dataContext.SaveChangesAsync();

        var request = new UpdateSeekerConfigRequest
        {
            SearchEnabled = true,
            SearchInterval = 3,
            ProactiveSearchEnabled = true,
            Instances =
            [
                // Update existing radarr config
                new UpdateSeekerInstanceConfigRequest
                {
                    ArrInstanceId = radarr.Id,
                    Enabled = true,
                    SkipTags = ["new-tag"],
                    ActiveDownloadLimit = 5,
                    MinCycleTimeDays = 14
                },
                // Create new sonarr config
                new UpdateSeekerInstanceConfigRequest
                {
                    ArrInstanceId = sonarr.Id,
                    Enabled = true,
                    SkipTags = ["sonarr-tag"],
                    ActiveDownloadLimit = 3,
                    MinCycleTimeDays = 7
                }
            ]
        };

        await _controller.UpdateSeekerConfig(request);

        var configs = await _dataContext.SeekerInstanceConfigs.ToListAsync();
        configs.Count.ShouldBe(2);

        var radarrConfig = configs.First(c => c.ArrInstanceId == radarr.Id);
        radarrConfig.Enabled.ShouldBeTrue();
        radarrConfig.SkipTags.ShouldContain("new-tag");
        radarrConfig.ActiveDownloadLimit.ShouldBe(5);
        radarrConfig.MinCycleTimeDays.ShouldBe(14);

        var sonarrConfig = configs.First(c => c.ArrInstanceId == sonarr.Id);
        sonarrConfig.Enabled.ShouldBeTrue();
        sonarrConfig.SkipTags.ShouldContain("sonarr-tag");
    }

    #endregion
}

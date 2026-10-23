using System.Text.Json;
using Cleanuparr.Api.Features.Seeker.Contracts.Requests;
using Cleanuparr.Api.Features.Seeker.Contracts.Responses;
using Cleanuparr.Api.Features.Seeker.Controllers;
using Cleanuparr.Api.Json;
using Cleanuparr.Api.Tests.Features.Seeker.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

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
        instance.IncludeTags.ShouldBeEmpty();
        instance.ActiveDownloadLimit.ShouldBe(3);
        instance.IgnoreStruckDownloads.ShouldBeFalse();
        instance.MinCycleTimeDays.ShouldBe(7);
        instance.RecentGrabWindowHours.ShouldBe(24);
        instance.SonarrSearchMode.ShouldBe(SonarrSearchMode.Auto);
    }

    [Fact]
    public async Task GetSeekerConfig_WithIgnoreStruckDownloadsEnabled_ReturnsEnabledValue()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarr.Id,
            Enabled = true,
            IgnoreStruckDownloads = true
        });
        await _dataContext.SaveChangesAsync();

        var result = await _controller.GetSeekerConfig();
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var response = okResult.Value.ShouldBeOfType<SeekerConfigResponse>();

        response.Instances.ShouldHaveSingleItem().IgnoreStruckDownloads.ShouldBeTrue();
    }

    [Fact]
    public async Task GetSeekerConfig_WithIncludeTags_ReturnsStoredTags()
    {
        ArrInstance radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarr.Id,
            Enabled = true,
            IncludeTags = ["wanted", "4k"]
        });
        await _dataContext.SaveChangesAsync();

        IActionResult result = await _controller.GetSeekerConfig();
        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        SeekerConfigResponse response = okResult.Value.ShouldBeOfType<SeekerConfigResponse>();

        response.Instances.ShouldHaveSingleItem().IncludeTags.ShouldBe(["wanted", "4k"]);
    }

    [Fact]
    public async Task GetSeekerConfig_WithRecentGrabWindowHours_ReturnsStoredValue()
    {
        ArrInstance radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarr.Id,
            Enabled = true,
            RecentGrabWindowHours = 48
        });
        await _dataContext.SaveChangesAsync();

        IActionResult result = await _controller.GetSeekerConfig();
        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        SeekerConfigResponse response = okResult.Value.ShouldBeOfType<SeekerConfigResponse>();

        response.Instances.ShouldHaveSingleItem().RecentGrabWindowHours.ShouldBe(48);
    }

    [Fact]
    public async Task GetSeekerConfig_WithRestSettings_ReturnsStoredValues()
    {
        ArrInstance radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarr.Id,
            Enabled = true,
            RestUnitsEnabled = true,
            RestMaxDays = 90
        });
        await _dataContext.SaveChangesAsync();

        IActionResult result = await _controller.GetSeekerConfig();
        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        SeekerConfigResponse response = okResult.Value.ShouldBeOfType<SeekerConfigResponse>();

        SeekerInstanceConfigResponse instance = response.Instances.ShouldHaveSingleItem();
        instance.RestUnitsEnabled.ShouldBeTrue();
        instance.RestMaxDays.ShouldBe(90);
    }

    [Fact]
    public async Task GetSeekerConfig_WithSonarrSearchMode_ReturnsStoredValue()
    {
        ArrInstance sonarr = SeekerTestDataFactory.AddSonarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarr.Id,
            Enabled = true,
            SonarrSearchMode = SonarrSearchMode.Episode
        });
        await _dataContext.SaveChangesAsync();

        IActionResult result = await _controller.GetSeekerConfig();
        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        SeekerConfigResponse response = okResult.Value.ShouldBeOfType<SeekerConfigResponse>();

        response.Instances.ShouldHaveSingleItem().SonarrSearchMode.ShouldBe(SonarrSearchMode.Episode);
    }

    [Fact]
    public async Task GetSeekerConfig_WithQuietHours_ReturnsStoredValues()
    {
        SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        SeekerConfig config = await _dataContext.SeekerConfigs.FirstAsync();
        config.QuietHoursEnabled = true;
        config.QuietHoursStart = new TimeOnly(22, 30);
        config.QuietHoursEnd = new TimeOnly(6, 15);
        await _dataContext.SaveChangesAsync();

        IActionResult result = await _controller.GetSeekerConfig();
        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        SeekerConfigResponse response = okResult.Value.ShouldBeOfType<SeekerConfigResponse>();

        response.QuietHoursEnabled.ShouldBeTrue();
        response.QuietHoursStart.ShouldBe(new TimeOnly(22, 30));
        response.QuietHoursEnd.ShouldBe(new TimeOnly(6, 15));
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

        await Should.ThrowAsync<CustomValidationException>(() => _controller.UpdateSeekerConfig(request));
    }

    [Fact]
    public async Task UpdateSeekerConfig_WithQuietHours_PersistsValues()
    {
        ArrInstance radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        UpdateSeekerConfigRequest request = new()
        {
            SearchEnabled = true,
            SearchInterval = 5,
            QuietHoursEnabled = true,
            QuietHoursStart = new TimeOnly(21, 0),
            QuietHoursEnd = new TimeOnly(8, 45),
            Instances = [new UpdateSeekerInstanceConfigRequest { ArrInstanceId = radarr.Id, Enabled = true }]
        };

        await _controller.UpdateSeekerConfig(request);

        SeekerConfig saved = await _dataContext.SeekerConfigs.AsNoTracking().FirstAsync();
        saved.QuietHoursEnabled.ShouldBeTrue();
        saved.QuietHoursStart.ShouldBe(new TimeOnly(21, 0));
        saved.QuietHoursEnd.ShouldBe(new TimeOnly(8, 45));
    }

    [Fact]
    public async Task UpdateSeekerConfig_WithEqualQuietHours_ThrowsValidationException()
    {
        ArrInstance radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        UpdateSeekerConfigRequest request = new()
        {
            SearchEnabled = true,
            SearchInterval = 5,
            QuietHoursEnabled = true,
            QuietHoursStart = new TimeOnly(7, 0),
            QuietHoursEnd = new TimeOnly(7, 0),
            Instances = [new UpdateSeekerInstanceConfigRequest { ArrInstanceId = radarr.Id, Enabled = true }]
        };

        await Should.ThrowAsync<CustomValidationException>(() => _controller.UpdateSeekerConfig(request));
    }

    [Theory]
    [InlineData("23:00")]
    [InlineData("23:00:00")]
    public void UpdateSeekerConfigRequest_DeserializesQuietHoursWithOrWithoutSeconds(string time)
    {
        JsonSerializerOptions options = new();
        CleanuparrJsonConfiguration.ConfigureApiInbound(options);

        UpdateSeekerConfigRequest? request = JsonSerializer.Deserialize<UpdateSeekerConfigRequest>(
            $"{{\"quietHoursEnabled\":true,\"quietHoursStart\":\"{time}\",\"quietHoursEnd\":\"07:00\"}}",
            options);

        request.ShouldNotBeNull();
        request.QuietHoursStart.ShouldBe(new TimeOnly(23, 0));
        request.QuietHoursEnd.ShouldBe(new TimeOnly(7, 0));
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

        // Keep interval at default (10)
        var request = new UpdateSeekerConfigRequest
        {
            SearchEnabled = true,
            SearchInterval = 10,
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

        // UseCustomFormatScore was false (default), now enable it on the instance
        var request = new UpdateSeekerConfigRequest
        {
            SearchEnabled = true,
            SearchInterval = 3,
            ProactiveSearchEnabled = true,
            Instances =
            [
                new UpdateSeekerInstanceConfigRequest { ArrInstanceId = radarr.Id, Enabled = true, UseCustomFormatScore = true }
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
            Enabled = true,
            UseCustomFormatScore = true
        });

        // Syncer was running: both proactive and CF score were enabled
        var config = await _dataContext.SeekerConfigs.FirstAsync();
        config.ProactiveSearchEnabled = true;
        await _dataContext.SaveChangesAsync();

        // Disable CF score — syncer conditions no longer met
        var request = new UpdateSeekerConfigRequest
        {
            SearchEnabled = true,
            SearchInterval = 3,
            ProactiveSearchEnabled = true,
            Instances =
            [
                new UpdateSeekerInstanceConfigRequest { ArrInstanceId = radarr.Id, Enabled = true, UseCustomFormatScore = false }
            ]
        };

        await _controller.UpdateSeekerConfig(request);

        await _jobManagementService.Received(1)
            .StopJob(JobType.CustomFormatScoreSyncer);
    }

    [Fact]
    public async Task UpdateSeekerConfig_WhenProactiveSearchDisabled_StopsSyncerJob()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarr.Id,
            Enabled = true,
            UseCustomFormatScore = true
        });

        // Syncer was running: both proactive and CF score were enabled
        var config = await _dataContext.SeekerConfigs.FirstAsync();
        config.ProactiveSearchEnabled = true;
        await _dataContext.SaveChangesAsync();

        // Disable proactive search — syncer should stop even though CF score is still enabled
        var request = new UpdateSeekerConfigRequest
        {
            SearchEnabled = true,
            SearchInterval = 3,
            ProactiveSearchEnabled = false,
            Instances =
            [
                new UpdateSeekerInstanceConfigRequest { ArrInstanceId = radarr.Id, Enabled = true, UseCustomFormatScore = true }
            ]
        };

        await _controller.UpdateSeekerConfig(request);

        await _jobManagementService.Received(1)
            .StopJob(JobType.CustomFormatScoreSyncer);
    }

    [Fact]
    public async Task UpdateSeekerConfig_WhenProactiveSearchEnabled_WithCfScoreActive_StartsAndTriggersSyncer()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarr.Id,
            Enabled = true,
            UseCustomFormatScore = true
        });

        // Syncer was NOT running: CF score enabled but proactive was off (default)
        await _dataContext.SaveChangesAsync();

        // Enable proactive search — syncer should start
        var request = new UpdateSeekerConfigRequest
        {
            SearchEnabled = true,
            SearchInterval = 3,
            ProactiveSearchEnabled = true,
            Instances =
            [
                new UpdateSeekerInstanceConfigRequest { ArrInstanceId = radarr.Id, Enabled = true, UseCustomFormatScore = true }
            ]
        };

        await _controller.UpdateSeekerConfig(request);

        await _jobManagementService.Received(1)
            .StartJob(JobType.CustomFormatScoreSyncer, null, Arg.Any<string>());
        await _jobManagementService.Received(1)
            .TriggerJobOnce(JobType.CustomFormatScoreSyncer);
    }

    [Fact]
    public async Task UpdateSeekerConfig_WhenCustomFormatScoreEnabledButProactiveDisabled_DoesNotStartSyncer()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarr.Id,
            Enabled = true,
            UseCustomFormatScore = false
        });

        // ProactiveSearchEnabled stays false (default)
        await _dataContext.SaveChangesAsync();

        // Enable CF score but keep proactive disabled — syncer should NOT start
        var request = new UpdateSeekerConfigRequest
        {
            SearchEnabled = true,
            SearchInterval = 3,
            ProactiveSearchEnabled = false,
            Instances =
            [
                new UpdateSeekerInstanceConfigRequest { ArrInstanceId = radarr.Id, Enabled = true, UseCustomFormatScore = true }
            ]
        };

        await _controller.UpdateSeekerConfig(request);

        await _jobManagementService.DidNotReceive()
            .StartJob(JobType.CustomFormatScoreSyncer, null, Arg.Any<string>());
        await _jobManagementService.DidNotReceive()
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
            IncludeTags = ["old-include"],
            ActiveDownloadLimit = 2,
            MinCycleTimeDays = 5,
            RecentGrabWindowHours = 12
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
                    IncludeTags = ["new-include"],
                    ActiveDownloadLimit = 5,
                    IgnoreStruckDownloads = true,
                    MinCycleTimeDays = 14,
                    RecentGrabWindowHours = 36
                },
                // Create new sonarr config
                new UpdateSeekerInstanceConfigRequest
                {
                    ArrInstanceId = sonarr.Id,
                    Enabled = true,
                    SkipTags = ["sonarr-tag"],
                    IncludeTags = ["sonarr-include"],
                    ActiveDownloadLimit = 3,
                    MinCycleTimeDays = 7,
                    RecentGrabWindowHours = 6,
                    SonarrSearchMode = SonarrSearchMode.Episode
                }
            ]
        };

        await _controller.UpdateSeekerConfig(request);

        var configs = await _dataContext.SeekerInstanceConfigs.ToListAsync();
        configs.Count.ShouldBe(2);

        var radarrConfig = configs.First(c => c.ArrInstanceId == radarr.Id);
        radarrConfig.Enabled.ShouldBeTrue();
        radarrConfig.SkipTags.ShouldContain("new-tag");
        radarrConfig.IncludeTags.ShouldBe(["new-include"]);
        radarrConfig.ActiveDownloadLimit.ShouldBe(5);
        radarrConfig.IgnoreStruckDownloads.ShouldBeTrue();
        radarrConfig.MinCycleTimeDays.ShouldBe(14);
        radarrConfig.RecentGrabWindowHours.ShouldBe(36);

        var sonarrConfig = configs.First(c => c.ArrInstanceId == sonarr.Id);
        sonarrConfig.Enabled.ShouldBeTrue();
        sonarrConfig.SkipTags.ShouldContain("sonarr-tag");
        sonarrConfig.IncludeTags.ShouldBe(["sonarr-include"]);
        sonarrConfig.IgnoreStruckDownloads.ShouldBeFalse();
        sonarrConfig.RecentGrabWindowHours.ShouldBe(6);
        sonarrConfig.SonarrSearchMode.ShouldBe(SonarrSearchMode.Episode);
    }

    [Fact]
    public async Task UpdateSeekerConfig_WithSonarrSearchMode_OnExistingInstance_PersistsValue()
    {
        ArrInstance sonarr = SeekerTestDataFactory.AddSonarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarr.Id,
            Enabled = true,
            SonarrSearchMode = SonarrSearchMode.Auto
        });
        await _dataContext.SaveChangesAsync();

        UpdateSeekerConfigRequest request = new()
        {
            SearchEnabled = true,
            SearchInterval = 3,
            ProactiveSearchEnabled = true,
            Instances =
            [
                new UpdateSeekerInstanceConfigRequest
                {
                    ArrInstanceId = sonarr.Id,
                    Enabled = true,
                    SonarrSearchMode = SonarrSearchMode.Season
                }
            ]
        };

        await _controller.UpdateSeekerConfig(request);

        SeekerInstanceConfig config = await _dataContext.SeekerInstanceConfigs.SingleAsync();
        config.SonarrSearchMode.ShouldBe(SonarrSearchMode.Season);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(169)]
    public async Task UpdateSeekerConfig_WithOutOfRangeRecentGrabWindowHours_OnExistingInstance_Throws(int recentGrabWindowHours)
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarr.Id,
            Enabled = true,
            RecentGrabWindowHours = 24
        });
        await _dataContext.SaveChangesAsync();

        var request = new UpdateSeekerConfigRequest
        {
            SearchEnabled = true,
            SearchInterval = 3,
            ProactiveSearchEnabled = true,
            Instances =
            [
                new UpdateSeekerInstanceConfigRequest
                {
                    ArrInstanceId = radarr.Id,
                    Enabled = true,
                    RecentGrabWindowHours = recentGrabWindowHours
                }
            ]
        };

        await Should.ThrowAsync<CustomValidationException>(() => _controller.UpdateSeekerConfig(request));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(169)]
    public async Task UpdateSeekerConfig_WithOutOfRangeRecentGrabWindowHours_OnNewInstance_ThrowsAndSavesNothing(int recentGrabWindowHours)
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);

        var request = new UpdateSeekerConfigRequest
        {
            SearchEnabled = true,
            SearchInterval = 3,
            ProactiveSearchEnabled = true,
            Instances =
            [
                new UpdateSeekerInstanceConfigRequest
                {
                    ArrInstanceId = radarr.Id,
                    Enabled = true,
                    RecentGrabWindowHours = recentGrabWindowHours
                }
            ]
        };

        await Should.ThrowAsync<CustomValidationException>(() => _controller.UpdateSeekerConfig(request));

        (await _dataContext.SeekerInstanceConfigs.AnyAsync(c => c.ArrInstanceId == radarr.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateSeekerConfig_WithRestSettings_OnExistingInstance_PersistsValues()
    {
        ArrInstance radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarr.Id,
            Enabled = true
        });
        await _dataContext.SaveChangesAsync();

        UpdateSeekerConfigRequest request = new()
        {
            SearchEnabled = true,
            SearchInterval = 3,
            ProactiveSearchEnabled = true,
            Instances =
            [
                new UpdateSeekerInstanceConfigRequest
                {
                    ArrInstanceId = radarr.Id,
                    Enabled = true,
                    RestUnitsEnabled = true,
                    RestMaxDays = 60
                }
            ]
        };

        await _controller.UpdateSeekerConfig(request);

        SeekerInstanceConfig config = await _dataContext.SeekerInstanceConfigs.SingleAsync();
        config.RestUnitsEnabled.ShouldBeTrue();
        config.RestMaxDays.ShouldBe(60);
    }

    [Fact]
    public async Task UpdateSeekerConfig_WithRestSettings_OnNewInstance_PersistsValues()
    {
        ArrInstance radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);

        UpdateSeekerConfigRequest request = new()
        {
            SearchEnabled = true,
            SearchInterval = 3,
            ProactiveSearchEnabled = true,
            Instances =
            [
                new UpdateSeekerInstanceConfigRequest
                {
                    ArrInstanceId = radarr.Id,
                    Enabled = true,
                    RestUnitsEnabled = true,
                    RestMaxDays = 365
                }
            ]
        };

        await _controller.UpdateSeekerConfig(request);

        SeekerInstanceConfig config = await _dataContext.SeekerInstanceConfigs.SingleAsync();
        config.RestUnitsEnabled.ShouldBeTrue();
        config.RestMaxDays.ShouldBe(365);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public async Task UpdateSeekerConfig_WithOutOfRangeRestMaxDays_OnExistingInstance_Throws(int restMaxDays)
    {
        ArrInstance radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarr.Id,
            Enabled = true
        });
        await _dataContext.SaveChangesAsync();

        UpdateSeekerConfigRequest request = new()
        {
            SearchEnabled = true,
            SearchInterval = 3,
            ProactiveSearchEnabled = true,
            Instances =
            [
                new UpdateSeekerInstanceConfigRequest
                {
                    ArrInstanceId = radarr.Id,
                    Enabled = true,
                    RestMaxDays = restMaxDays
                }
            ]
        };

        await Should.ThrowAsync<CustomValidationException>(() => _controller.UpdateSeekerConfig(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public async Task UpdateSeekerConfig_WithOutOfRangeRestMaxDays_OnNewInstance_ThrowsAndSavesNothing(int restMaxDays)
    {
        ArrInstance radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);

        UpdateSeekerConfigRequest request = new()
        {
            SearchEnabled = true,
            SearchInterval = 3,
            ProactiveSearchEnabled = true,
            Instances =
            [
                new UpdateSeekerInstanceConfigRequest
                {
                    ArrInstanceId = radarr.Id,
                    Enabled = true,
                    RestMaxDays = restMaxDays
                }
            ]
        };

        await Should.ThrowAsync<CustomValidationException>(() => _controller.UpdateSeekerConfig(request));

        (await _dataContext.SeekerInstanceConfigs.AnyAsync(c => c.ArrInstanceId == radarr.Id)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(168)]
    public async Task UpdateSeekerConfig_WithBoundaryRecentGrabWindowHours_Succeeds(int recentGrabWindowHours)
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);

        var request = new UpdateSeekerConfigRequest
        {
            SearchEnabled = true,
            SearchInterval = 3,
            ProactiveSearchEnabled = true,
            Instances =
            [
                new UpdateSeekerInstanceConfigRequest
                {
                    ArrInstanceId = radarr.Id,
                    Enabled = true,
                    RecentGrabWindowHours = recentGrabWindowHours
                }
            ]
        };

        await _controller.UpdateSeekerConfig(request);

        var config = await _dataContext.SeekerInstanceConfigs.FirstAsync(c => c.ArrInstanceId == radarr.Id);
        config.RecentGrabWindowHours.ShouldBe(recentGrabWindowHours);
    }

    #endregion
}

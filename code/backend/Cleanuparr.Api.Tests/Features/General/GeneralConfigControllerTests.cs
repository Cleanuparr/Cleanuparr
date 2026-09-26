using Cleanuparr.Api.Features.General.Contracts.Requests;
using Cleanuparr.Api.Features.General.Contracts.Responses;
using Cleanuparr.Api.Features.General.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.State;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Features.General;

public class GeneralConfigControllerTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly EventsContext _eventsContext;
    private readonly IDynamicHttpClientFactory _dynamicHttpClientFactory;
    private readonly GeneralConfigController _controller;

    public GeneralConfigControllerTests()
    {
        _dataContext = ConfigControllerTestDataFactory.CreateDataContext();
        _eventsContext = ConfigControllerTestDataFactory.CreateEventsContext();
        _dynamicHttpClientFactory = Substitute.For<IDynamicHttpClientFactory>();

        var logger = Substitute.For<ILogger<GeneralConfigController>>();
        _controller = new GeneralConfigController(logger, _dataContext);

        // Mount a DefaultHttpContext with a ServiceProvider that resolves IDynamicHttpClientFactory
        var services = new ServiceCollection();
        services.AddSingleton(_dynamicHttpClientFactory);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() },
        };

        Striker.RecurringHashes.Clear();
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        _eventsContext.Dispose();
        Striker.RecurringHashes.Clear();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetGeneralConfig_ReturnsExistingConfig()
    {
        // Act
        var result = await _controller.GetGeneralConfig();

        // Assert
        var ok = result.ShouldBeOfType<OkObjectResult>();
        ok.Value.ShouldBeOfType<GeneralConfigResponse>();
    }

    [Fact]
    public async Task UpdateGeneralConfig_PersistsChangesAndUpdatesHttpClients()
    {
        // Arrange — keep Log defaults matching DB so loggingChanged=false (avoid LoggingConfigManager statics)
        var existing = await _dataContext.GeneralConfigs.AsNoTracking().FirstAsync();
        var request = new UpdateGeneralConfigRequest
        {
            DisplaySupportBanner = false,
            DryRun = false,
            HttpMaxRetries = 5,
            HttpTimeout = 60,
            HttpSendUserAgent = true,
            StatusCheckEnabled = false,
            IgnoredDownloads = new List<string> { "ignored-item" },
            StrikeInactivityWindowHours = 48,
            Log = MatchingLogRequest(existing.Log),
            Auth = new UpdateAuthConfigRequest(),
        };

        // Act
        var result = await _controller.UpdateGeneralConfig(request, _eventsContext);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        _dynamicHttpClientFactory.Received(1).UpdateAllClientsFromGeneralConfig(Arg.Any<GeneralConfig>());

        var saved = await _dataContext.GeneralConfigs.AsNoTracking().FirstAsync();
        saved.DisplaySupportBanner.ShouldBeFalse();
        saved.HttpMaxRetries.ShouldBe((ushort)5);
        saved.HttpTimeout.ShouldBe((ushort)60);
        saved.HttpSendUserAgent.ShouldBeTrue();
        saved.StrikeInactivityWindowHours.ShouldBe((ushort)48);
        saved.IgnoredDownloads.ShouldContain("ignored-item");
    }

    [Fact]
    public async Task UpdateGeneralConfig_InvalidHttpTimeout_Throws()
    {
        // Arrange — HttpTimeout=0 fails validation
        var existing = await _dataContext.GeneralConfigs.AsNoTracking().FirstAsync();
        var request = new UpdateGeneralConfigRequest
        {
            HttpTimeout = 0,
            StrikeInactivityWindowHours = 24,
            Log = MatchingLogRequest(existing.Log),
            Auth = new UpdateAuthConfigRequest(),
        };

        // Act / Assert
        await Should.ThrowAsync<Exception>(() => _controller.UpdateGeneralConfig(request, _eventsContext));
    }

    [Fact]
    public async Task UpdateGeneralConfig_InvalidStrikeWindow_Throws()
    {
        // Arrange — StrikeInactivityWindowHours > 168 fails validation
        var existing = await _dataContext.GeneralConfigs.AsNoTracking().FirstAsync();
        var request = new UpdateGeneralConfigRequest
        {
            HttpTimeout = 60,
            StrikeInactivityWindowHours = 200,
            Log = MatchingLogRequest(existing.Log),
            Auth = new UpdateAuthConfigRequest(),
        };

        // Act / Assert
        await Should.ThrowAsync<Exception>(() => _controller.UpdateGeneralConfig(request, _eventsContext));
    }

    [Fact]
    public async Task UpdateGeneralConfig_ConnectivityCheckEnabledWithNoUrls_Throws()
    {
        // Arrange — connectivity check enabled but no URLs fails validation
        var existing = await _dataContext.GeneralConfigs.AsNoTracking().FirstAsync();
        var request = new UpdateGeneralConfigRequest
        {
            HttpTimeout = 60,
            StrikeInactivityWindowHours = 24,
            ConnectivityCheckEnabled = true,
            ConnectivityCheckUrls = new List<string>(),
            Log = MatchingLogRequest(existing.Log),
            Auth = new UpdateAuthConfigRequest(),
        };

        // Act / Assert
        await Should.ThrowAsync<Exception>(() => _controller.UpdateGeneralConfig(request, _eventsContext));
    }

    [Fact]
    public async Task UpdateGeneralConfig_DryRunDisabled_ClearsWhatTheDryRunLeftBehind()
    {
        // Arrange
        var jobRun = new JobRun { Id = Guid.NewGuid(), Type = JobType.QueueCleaner };
        _eventsContext.JobRuns.Add(jobRun);

        // Real strike plus a dry-run strike: purge clears the mark
        var touchedByDryRun = new DownloadItem
        {
            DownloadId = "touched-by-dry-run",
            Title = "Struck for real, then by a dry run",
            IsMarkedForRemoval = true,
        };

        // Real strikes only: purge keeps its flags
        var realOnly = new DownloadItem
        {
            DownloadId = "real-only",
            Title = "Struck only by real runs",
            IsMarkedForRemoval = true,
            IsRemoved = true,
            IsReturning = true,
        };
        _eventsContext.DownloadItems.AddRange(touchedByDryRun, realOnly);

        _eventsContext.Strikes.AddRange(
            new Strike { DownloadItemId = touchedByDryRun.Id, JobRunId = jobRun.Id, Type = StrikeType.FailedImport },
            new Strike { DownloadItemId = touchedByDryRun.Id, JobRunId = jobRun.Id, Type = StrikeType.FailedImport, IsDryRun = true },
            new Strike { DownloadItemId = realOnly.Id, JobRunId = jobRun.Id, Type = StrikeType.FailedImport });
        await _eventsContext.SaveChangesAsync();

        Striker.RecurringHashes.TryAdd("genuine-recurrence", null);

        var config = await _dataContext.GeneralConfigs.FirstAsync();
        config.DryRun = true;
        await _dataContext.SaveChangesAsync();

        var request = new UpdateGeneralConfigRequest
        {
            DryRun = false,
            HttpTimeout = 60,
            StrikeInactivityWindowHours = 24,
            Log = MatchingLogRequest(config.Log),
            Auth = new UpdateAuthConfigRequest(),
        };

        // Act
        await _controller.UpdateGeneralConfig(request, _eventsContext);

        // Assert
        _eventsContext.ChangeTracker.Clear();
        (await _eventsContext.Strikes.CountAsync(x => x.IsDryRun)).ShouldBe(0);

        DownloadItem touched = await _eventsContext.DownloadItems.AsNoTracking().FirstAsync(x => x.DownloadId == "touched-by-dry-run");
        touched.IsMarkedForRemoval.ShouldBeFalse();

        // Real-run flags survive the purge
        DownloadItem real = await _eventsContext.DownloadItems.AsNoTracking().FirstAsync(x => x.DownloadId == "real-only");
        real.IsMarkedForRemoval.ShouldBeTrue();
        real.IsRemoved.ShouldBeTrue();
        real.IsReturning.ShouldBeTrue();

        // A recurrence from before the purge survives it
        Striker.RecurringHashes.ShouldContainKey("genuine-recurrence");
    }

    [Fact]
    public async Task PurgeAllStrikes_ReturnsDeletedCounts()
    {
        // Act
        var result = await _controller.PurgeAllStrikes(_eventsContext);

        // Assert — initially empty, but the endpoint still succeeds with zero counts
        var ok = result.ShouldBeOfType<OkObjectResult>();
        ok.Value.ShouldNotBeNull();
    }

    private static UpdateLoggingConfigRequest MatchingLogRequest(LoggingConfig existing) => new()
    {
        Level = existing.Level,
        RollingSizeMB = existing.RollingSizeMB,
        RetainedFileCount = existing.RetainedFileCount,
        TimeLimitHours = existing.TimeLimitHours,
        ArchiveEnabled = existing.ArchiveEnabled,
        ArchiveRetainedCount = existing.ArchiveRetainedCount,
        ArchiveTimeLimitHours = existing.ArchiveTimeLimitHours,
    };
}

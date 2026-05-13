using Cleanuparr.Api.Features.General.Contracts.Requests;
using Cleanuparr.Api.Features.General.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.General;
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
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        _eventsContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetGeneralConfig_ReturnsExistingConfig()
    {
        // Act
        var result = await _controller.GetGeneralConfig();

        // Assert
        var ok = result.ShouldBeOfType<OkObjectResult>();
        ok.Value.ShouldBeOfType<GeneralConfig>();
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
            StatusCheckEnabled = false,
            EncryptionKey = existing.EncryptionKey,
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
            EncryptionKey = existing.EncryptionKey,
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
            EncryptionKey = existing.EncryptionKey,
            StrikeInactivityWindowHours = 200,
            Log = MatchingLogRequest(existing.Log),
            Auth = new UpdateAuthConfigRequest(),
        };

        // Act / Assert
        await Should.ThrowAsync<Exception>(() => _controller.UpdateGeneralConfig(request, _eventsContext));
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

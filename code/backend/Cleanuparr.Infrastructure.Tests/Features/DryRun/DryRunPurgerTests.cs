using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DryRun;
using Cleanuparr.Infrastructure.Tests.Features.Arr;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.Events;
using Cleanuparr.Persistence.Models.State;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DryRun;

[Collection(ForceImportDryRunCollection.Name)]
public sealed class DryRunPurgerTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly EventsContext _eventsContext;
    private readonly DryRunPurger _purger;

    public DryRunPurgerTests()
    {
        _dataContext = TestDataContextFactory.Create();
        _eventsContext = TestDataContextFactory.CreateEvents();
        _purger = new DryRunPurger(Substitute.For<ILogger<DryRunPurger>>(), _dataContext, _eventsContext);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        _eventsContext.Dispose();
    }

    private async Task SetDbDryRun(bool value)
    {
        GeneralConfig config = await _dataContext.GeneralConfigs.SingleAsync();
        config.DryRun = value;
        await _dataContext.SaveChangesAsync();
    }

    private async Task AddDryRunStrikeAsync()
    {
        var jobRun = new JobRun { Id = Guid.NewGuid(), Type = JobType.QueueCleaner };
        var downloadItem = new DownloadItem { DownloadId = Guid.NewGuid().ToString(), Title = "dry run strike target" };
        _eventsContext.JobRuns.Add(jobRun);
        _eventsContext.DownloadItems.Add(downloadItem);
        _eventsContext.Strikes.Add(new Strike
        {
            DownloadItemId = downloadItem.Id,
            JobRunId = jobRun.Id,
            Type = StrikeType.FailedImport,
            IsDryRun = true,
        });
        await _eventsContext.SaveChangesAsync();
    }

    [Fact]
    public async Task PurgeAsync_ClearsWhatTheDryRunLeftBehind()
    {
        // Arrange
        var jobRun = new JobRun { Id = Guid.NewGuid(), Type = JobType.QueueCleaner };
        _eventsContext.JobRuns.Add(jobRun);

        // Real strike plus a dry-run strike: purge keeps the mark
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

        _eventsContext.SeekerHistory.AddRange(
            new SeekerHistory { ItemTitle = "dry run history", IsDryRun = true, CycleId = Guid.NewGuid() },
            new SeekerHistory { ItemTitle = "real history", IsDryRun = false, CycleId = Guid.NewGuid() });

        await _eventsContext.SaveChangesAsync();

        // Act
        await _purger.PurgeAsync();

        // Assert
        _eventsContext.ChangeTracker.Clear();
        (await _eventsContext.Strikes.CountAsync(x => x.IsDryRun)).ShouldBe(0);
        (await _eventsContext.SeekerHistory.CountAsync(x => x.IsDryRun)).ShouldBe(0);
        (await _eventsContext.SeekerHistory.CountAsync()).ShouldBe(1);

        DownloadItem touched = await _eventsContext.DownloadItems.AsNoTracking().FirstAsync(x => x.DownloadId == "touched-by-dry-run");
        touched.IsMarkedForRemoval.ShouldBeTrue();

        // Real-run flags survive the purge
        DownloadItem real = await _eventsContext.DownloadItems.AsNoTracking().FirstAsync(x => x.DownloadId == "real-only");
        real.IsMarkedForRemoval.ShouldBeTrue();
        real.IsRemoved.ShouldBeTrue();
        real.IsReturning.ShouldBeTrue();
    }

    [Fact]
    public async Task PurgeIfDryRunOffAsync_DbDryRunTrue_DoesNotPurge()
    {
        // Arrange
        await SetDbDryRun(true);
        await AddDryRunStrikeAsync();

        // Act
        await _purger.PurgeIfDryRunOffAsync();

        // Assert
        (await _eventsContext.Strikes.CountAsync(x => x.IsDryRun)).ShouldBe(1);
    }

    [Fact]
    public async Task PurgeIfDryRunOffAsync_DbDryRunFalse_Purges()
    {
        // Arrange
        await SetDbDryRun(false);
        await AddDryRunStrikeAsync();

        // Act
        await _purger.PurgeIfDryRunOffAsync();

        // Assert
        (await _eventsContext.Strikes.CountAsync(x => x.IsDryRun)).ShouldBe(0);
    }

    [Fact]
    public async Task PurgeIfDryRunOffAsync_IgnoresStickyValue_UsesRawDbRead()
    {
        // Arrange - sticky says on, database says off
        await SetDbDryRun(false);
        ContextProvider.SetDryRun(true);
        await AddDryRunStrikeAsync();

        // Act
        await _purger.PurgeIfDryRunOffAsync();

        // Assert
        (await _eventsContext.Strikes.CountAsync(x => x.IsDryRun)).ShouldBe(0);
    }
}

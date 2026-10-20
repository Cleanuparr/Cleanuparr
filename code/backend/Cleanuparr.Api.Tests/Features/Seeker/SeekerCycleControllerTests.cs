using Cleanuparr.Api.Features.Seeker.Controllers;
using Cleanuparr.Api.Tests.Features.Seeker.TestHelpers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Seeker;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.State;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Cleanuparr.Api.Tests.Features.Seeker;

public class SeekerCycleControllerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly DataContext _dataContext;
    private readonly EventsContext _eventsContext;
    private readonly SeekerCycleLock _cycleLock = new();
    private readonly SeekerCycleController _controller;

    public SeekerCycleControllerTests()
    {
        _dataContext = SeekerTestDataFactory.CreateDataContext();
        _eventsContext = SeekerTestDataFactory.CreateEventsContext();
        _controller = new SeekerCycleController(
            NullLogger<SeekerCycleController>.Instance,
            _dataContext,
            _eventsContext,
            new FakeTimeProvider(Now),
            _cycleLock);
        ConfigControllerTestDataFactory.ConfigureProblemDetails(_controller);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        _eventsContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ResetCycle_OpenCycle_ClosesItAsResetAndMakesTheNewCycleCurrent()
    {
        ArrInstance radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        Guid oldCycleId = Guid.NewGuid();
        AddInstanceConfig(radarr.Id, oldCycleId);
        AddCycle(oldCycleId, radarr.Id, startedAt: Now.AddDays(-2), searchedUnits: 3, eligibleNow: 10);

        IActionResult result = await _controller.ResetCycle(radarr.Id);

        result.ShouldBeOfType<NoContentResult>();
        SeekerCycle old = await _eventsContext.SeekerCycles.AsNoTracking().SingleAsync(c => c.Id == oldCycleId);
        old.CompletedAt.ShouldBe(Now);
        old.CompletionReason.ShouldBe(SeekerCycleCompletionReason.Reset);

        SeekerInstanceConfig config = await _dataContext.SeekerInstanceConfigs.AsNoTracking().SingleAsync();
        config.CurrentCycleId.ShouldNotBe(oldCycleId);

        SeekerCycle next = await _eventsContext.SeekerCycles.AsNoTracking().SingleAsync(c => c.Id == config.CurrentCycleId);
        next.ArrInstanceId.ShouldBe(radarr.Id);
        next.StartedAt.ShouldBe(Now);
        next.CompletedAt.ShouldBeNull();
        next.SearchedUnits.ShouldBe(0);
        next.EligibleAtStart.ShouldBe(10);
        next.EligibleNow.ShouldBe(10);
        next.RemainingUnits.ShouldBe(10);
        next.IsDryRun.ShouldBeFalse();
    }

    [Fact]
    public async Task ResetCycle_CycleWaitingOnMinCycleTime_KeepsItsCompletedReason()
    {
        ArrInstance radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        Guid oldCycleId = Guid.NewGuid();
        AddInstanceConfig(radarr.Id, oldCycleId);
        AddCycle(oldCycleId, radarr.Id, startedAt: Now.AddDays(-2), searchedUnits: 3, completedAt: Now.AddDays(-1));

        IActionResult result = await _controller.ResetCycle(radarr.Id);

        result.ShouldBeOfType<NoContentResult>();
        SeekerCycle old = await _eventsContext.SeekerCycles.AsNoTracking().SingleAsync(c => c.Id == oldCycleId);
        old.CompletedAt.ShouldBe(Now.AddDays(-1));
        old.CompletionReason.ShouldBe(SeekerCycleCompletionReason.Completed);

        SeekerInstanceConfig config = await _dataContext.SeekerInstanceConfigs.AsNoTracking().SingleAsync();
        config.CurrentCycleId.ShouldNotBe(oldCycleId);
        (await _eventsContext.SeekerCycles.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task ResetCycle_NothingSearchedYet_ReturnsConflictAndChangesNothing()
    {
        ArrInstance radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        Guid cycleId = Guid.NewGuid();
        AddInstanceConfig(radarr.Id, cycleId);
        AddCycle(cycleId, radarr.Id, startedAt: Now.AddDays(-2), searchedUnits: 0);

        IActionResult result = await _controller.ResetCycle(radarr.Id);

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        (await _eventsContext.SeekerCycles.CountAsync()).ShouldBe(1);
        (await _dataContext.SeekerInstanceConfigs.AsNoTracking().SingleAsync()).CurrentCycleId.ShouldBe(cycleId);
    }

    [Fact]
    public async Task ResetCycle_NoCycleRow_ReturnsConflict()
    {
        ArrInstance radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        AddInstanceConfig(radarr.Id, Guid.NewGuid());

        IActionResult result = await _controller.ResetCycle(radarr.Id);

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        (await _eventsContext.SeekerCycles.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task ResetCycle_SeekerProcessingInstance_ReturnsConflictAndChangesNothing()
    {
        ArrInstance radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        Guid cycleId = Guid.NewGuid();
        AddInstanceConfig(radarr.Id, cycleId);
        AddCycle(cycleId, radarr.Id, startedAt: Now.AddDays(-2), searchedUnits: 3);
        _cycleLock.TryEnter(radarr.Id).ShouldBeTrue();

        IActionResult result = await _controller.ResetCycle(radarr.Id);

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        (await _eventsContext.SeekerCycles.AsNoTracking().SingleAsync()).CompletedAt.ShouldBeNull();
        (await _dataContext.SeekerInstanceConfigs.AsNoTracking().SingleAsync()).CurrentCycleId.ShouldBe(cycleId);
    }

    [Fact]
    public async Task ResetCycle_AfterReset_ReleasesTheLock()
    {
        ArrInstance radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        Guid cycleId = Guid.NewGuid();
        AddInstanceConfig(radarr.Id, cycleId);
        AddCycle(cycleId, radarr.Id, startedAt: Now.AddDays(-2), searchedUnits: 3);

        await _controller.ResetCycle(radarr.Id);

        _cycleLock.TryEnter(radarr.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task ResetCycle_UnknownInstance_ReturnsNotFound()
    {
        IActionResult result = await _controller.ResetCycle(Guid.NewGuid());

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ResetCycle_DryRunOn_ResetsTheNewestDryCycleAndLeavesTheLiveCycleAlone()
    {
        ArrInstance radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        (await _dataContext.GeneralConfigs.FirstAsync()).DryRun = true;
        await _dataContext.SaveChangesAsync();

        Guid liveCycleId = Guid.NewGuid();
        AddInstanceConfig(radarr.Id, liveCycleId);
        AddCycle(liveCycleId, radarr.Id, startedAt: Now.AddDays(-5), searchedUnits: 4);

        Guid olderDryCycleId = Guid.NewGuid();
        Guid dryCycleId = Guid.NewGuid();
        AddCycle(olderDryCycleId, radarr.Id, startedAt: Now.AddDays(-9), searchedUnits: 2, completedAt: Now.AddDays(-8), isDryRun: true);
        AddCycle(dryCycleId, radarr.Id, startedAt: Now.AddDays(-1), searchedUnits: 2, eligibleNow: 6, isDryRun: true);

        IActionResult result = await _controller.ResetCycle(radarr.Id);

        result.ShouldBeOfType<NoContentResult>();
        SeekerCycle dry = await _eventsContext.SeekerCycles.AsNoTracking().SingleAsync(c => c.Id == dryCycleId);
        dry.CompletionReason.ShouldBe(SeekerCycleCompletionReason.Reset);

        SeekerCycle live = await _eventsContext.SeekerCycles.AsNoTracking().SingleAsync(c => c.Id == liveCycleId);
        live.CompletedAt.ShouldBeNull();
        (await _dataContext.SeekerInstanceConfigs.AsNoTracking().SingleAsync()).CurrentCycleId.ShouldBe(liveCycleId);

        SeekerCycle next = await _eventsContext.SeekerCycles.AsNoTracking()
            .Where(c => c.IsDryRun)
            .OrderByDescending(c => c.StartedAt)
            .FirstAsync();
        next.Id.ShouldNotBe(dryCycleId);
        next.StartedAt.ShouldBe(Now);
        next.EligibleNow.ShouldBe(6);
    }

    private void AddInstanceConfig(Guid arrInstanceId, Guid currentCycleId)
    {
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = arrInstanceId,
            Enabled = true,
            CurrentCycleId = currentCycleId,
        });
        _dataContext.SaveChanges();
    }

    private void AddCycle(
        Guid id,
        Guid arrInstanceId,
        DateTimeOffset startedAt,
        int searchedUnits,
        int eligibleNow = 5,
        DateTimeOffset? completedAt = null,
        bool isDryRun = false)
    {
        _eventsContext.SeekerCycles.Add(new SeekerCycle
        {
            Id = id,
            ArrInstanceId = arrInstanceId,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            CompletionReason = completedAt is null ? null : SeekerCycleCompletionReason.Completed,
            EligibleAtStart = eligibleNow,
            EligibleNow = eligibleNow,
            RemainingUnits = completedAt is null ? eligibleNow - searchedUnits : 0,
            SearchedUnits = searchedUnits,
            IsDryRun = isDryRun,
        });
        _eventsContext.SaveChanges();
    }
}

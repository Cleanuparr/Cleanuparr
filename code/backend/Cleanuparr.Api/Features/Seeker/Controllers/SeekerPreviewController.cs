using Cleanuparr.Api.Extensions;
using Cleanuparr.Api.Features.Seeker.Contracts.Responses;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Seeker;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.State;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Features.Seeker.Controllers;

[ApiController]
[Route("api/seeker/preview")]
[Authorize]
public sealed class SeekerPreviewController : ControllerBase
{
    private const int NextUpCount = 10;

    private readonly ILogger<SeekerPreviewController> _logger;
    private readonly DataContext _dataContext;
    private readonly EventsContext _eventsContext;
    private readonly IArrClientFactory _arrClientFactory;
    private readonly IArrQueueReader _arrQueueReader;
    private readonly SeekerUnitEvaluator _evaluator;
    private readonly TimeProvider _timeProvider;

    public SeekerPreviewController(
        ILogger<SeekerPreviewController> logger,
        DataContext dataContext,
        EventsContext eventsContext,
        IArrClientFactory arrClientFactory,
        IArrQueueReader arrQueueReader,
        SeekerUnitEvaluator evaluator,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _eventsContext = eventsContext;
        _arrClientFactory = arrClientFactory;
        _arrQueueReader = arrQueueReader;
        _evaluator = evaluator;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Runs the instance through the Seeker filters without searching or saving anything.
    /// Lists the units the next runs would search and why everything else is left out.
    /// </summary>
    [HttpGet("{arrInstanceId:guid}")]
    public async Task<IActionResult> GetPreview(Guid arrInstanceId, CancellationToken cancellationToken)
    {
        SeekerInstanceConfig? instanceConfig = await _dataContext.SeekerInstanceConfigs
            .AsNoTracking()
            .Include(s => s.ArrInstance.ArrConfig)
            .FirstOrDefaultAsync(s => s.ArrInstanceId == arrInstanceId, cancellationToken);

        if (instanceConfig is null)
        {
            return this.ProblemResult(StatusCodes.Status404NotFound, $"Seeker instance with ID {arrInstanceId} not found");
        }

        InstanceType instanceType = instanceConfig.ArrInstance.ArrConfig.Type;
        if (instanceType is not (InstanceType.Sonarr or InstanceType.Radarr))
        {
            return this.ProblemResult(StatusCodes.Status400BadRequest, $"Seeker does not search {instanceType} instances");
        }

        SeekerConfig config = await _dataContext.SeekerConfigs.AsNoTracking().FirstAsync(cancellationToken);
        bool isDryRun = (await _dataContext.GeneralConfigs.AsNoTracking().FirstAsync(cancellationToken)).DryRun;

        SeekerCycle? cycle = isDryRun
            ? await _eventsContext.SeekerCycles
                .AsNoTracking()
                .Where(c => c.ArrInstanceId == arrInstanceId && c.IsDryRun)
                .OrderByDescending(c => c.StartedAt)
                .FirstOrDefaultAsync(cancellationToken)
            : await _eventsContext.SeekerCycles
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == instanceConfig.CurrentCycleId && !c.IsDryRun, cancellationToken);

        List<SeekerHistory> cycleHistory = cycle is null
            ? []
            : await _eventsContext.SeekerHistory
                .AsNoTracking()
                .Where(h => h.ArrInstanceId == arrInstanceId && h.CycleId == cycle.Id && h.IsDryRun == isDryRun)
                .ToListAsync(cancellationToken);

        SeekerEvaluation evaluation;
        try
        {
            IArrClient arrClient = _arrClientFactory.GetClient(instanceType, instanceConfig.ArrInstance.Version);
            List<QueueRecord> queueRecords = await _arrQueueReader.ReadAllAsync(arrClient, instanceConfig.ArrInstance);
            SeekerRecentGrabs recentGrabs = await _evaluator.ReadRecentGrabsAsync(instanceConfig, instanceConfig.ArrInstance, cancellationToken);

            evaluation = await _evaluator.EvaluateAsync(
                config, instanceConfig, instanceConfig.ArrInstance, queueRecords, recentGrabs, cycleHistory, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build the Seeker preview for {InstanceName}", instanceConfig.ArrInstance.Name);
            return this.ProblemResult(StatusCodes.Status502BadGateway, $"Could not read {instanceConfig.ArrInstance.Name}: {ex.Message}");
        }

        if (evaluation.WantedUnsupported)
        {
            return this.ProblemResult(StatusCodes.Status502BadGateway, "Seeker needs Radarr v5.6 or newer");
        }

        DateTimeOffset? waitingUntil = null;

        // Same rollover as the Seeker job: a cycle with nothing left waits out the minimum cycle time, then starts over.
        if (evaluation.Remaining.Count == 0 && cycle is { SearchedUnits: > 0 })
        {
            DateTimeOffset minCycleEnd = cycle.StartedAt.AddDays(instanceConfig.MinCycleTimeDays);
            if (_timeProvider.GetUtcNow() < minCycleEnd)
            {
                waitingUntil = minCycleEnd;
            }
            else
            {
                evaluation = evaluation.WithoutCycleHistory();
            }
        }

        InstanceFunnelStat funnel = ToFunnel(evaluation);

        List<SeekerSearchCandidate> nextUp = waitingUntil is null
            ? SeekerUnitEvaluator.SelectNext(evaluation, config.SelectionStrategy, NextUpCount)
            : [];

        HashSet<long> nextUpIds = nextUp.Select(c => c.ItemId).ToHashSet();
        bool randomSeason = evaluation.Remaining
            .Where(u => nextUpIds.Contains(u.ItemId))
            .GroupBy(u => u.ItemId)
            .Any(g => g.Select(u => u.SeasonNumber).Distinct().Count() > 1);

        return Ok(new SeekerPreviewResponse
        {
            Funnel = funnel,
            CycleWaitingUntil = waitingUntil,
            RandomPick = randomSeason || config.SelectionStrategy is not (SelectionStrategy.OldestSearchFirst or SelectionStrategy.NewestFirst),
            NextUp = nextUp
                .Select(c => new SeekerPreviewUnitResponse { Name = c.Name, Reason = c.Reason, ContentKind = c.ContentKind })
                .ToList(),
            Excluded = evaluation.Excluded
                .Select(e => new SeekerPreviewExclusionResponse { Name = e.Name, Reason = e.Reason })
                .ToList(),
        });
    }

    private InstanceFunnelStat ToFunnel(SeekerEvaluation evaluation)
    {
        int remaining = evaluation.Remaining.Count;

        return new InstanceFunnelStat
        {
            LibraryItems = evaluation.AllLibraryIds.Count,
            ReleasedItems = evaluation.ReleasedItems,
            MonitoredItems = evaluation.MonitoredItems,
            TagFilteredItems = evaluation.TagFilteredItems,
            EligibleUnits = evaluation.Units.Count,
            QueuedUnits = evaluation.QueuedUnits,
            RecentlyGrabbedUnits = evaluation.RecentlyGrabbedUnits,
            SearchedUnits = evaluation.Units.Count - evaluation.QueuedUnits - evaluation.RecentlyGrabbedUnits - remaining,
            RemainingUnits = remaining,
            CountedAt = _timeProvider.GetUtcNow(),
        };
    }
}

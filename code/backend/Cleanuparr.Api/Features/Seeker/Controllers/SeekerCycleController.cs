using Cleanuparr.Api.Extensions;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Seeker;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.State;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Features.Seeker.Controllers;

[ApiController]
[Route("api/seeker/cycles")]
[Authorize]
public sealed class SeekerCycleController : ControllerBase
{
    private readonly ILogger<SeekerCycleController> _logger;
    private readonly DataContext _dataContext;
    private readonly EventsContext _eventsContext;
    private readonly TimeProvider _timeProvider;
    private readonly SeekerCycleLock _cycleLock;

    public SeekerCycleController(
        ILogger<SeekerCycleController> logger,
        DataContext dataContext,
        EventsContext eventsContext,
        TimeProvider timeProvider,
        SeekerCycleLock cycleLock)
    {
        _logger = logger;
        _dataContext = dataContext;
        _eventsContext = eventsContext;
        _timeProvider = timeProvider;
        _cycleLock = cycleLock;
    }

    /// <summary>
    /// Closes the instance's current cycle and starts a new one, skipping the minimum cycle time once.
    /// Dry run resets its own cycle and leaves the live one alone.
    /// </summary>
    [HttpPost("{arrInstanceId:guid}/reset")]
    public async Task<IActionResult> ResetCycle(Guid arrInstanceId)
    {
        if (!_cycleLock.TryEnter(arrInstanceId))
        {
            return this.ProblemResult(StatusCodes.Status409Conflict, "The Seeker is searching this instance, try again in a moment");
        }

        try
        {
            SeekerInstanceConfig? instanceConfig = await _dataContext.SeekerInstanceConfigs
                .Include(s => s.ArrInstance)
                .FirstOrDefaultAsync(s => s.ArrInstanceId == arrInstanceId);

            if (instanceConfig is null)
            {
                return this.ProblemResult(StatusCodes.Status404NotFound, $"Seeker instance with ID {arrInstanceId} not found");
            }

            bool isDryRun = (await _dataContext.GeneralConfigs.AsNoTracking().FirstAsync()).DryRun;
            SeekerCycle? cycle = isDryRun
                ? await _eventsContext.SeekerCycles
                    .Where(c => c.ArrInstanceId == arrInstanceId && c.IsDryRun)
                    .OrderByDescending(c => c.StartedAt)
                    .FirstOrDefaultAsync()
                : await _eventsContext.SeekerCycles
                    .FirstOrDefaultAsync(c => c.Id == instanceConfig.CurrentCycleId && !c.IsDryRun);

            if (cycle is null || cycle.SearchedUnits == 0)
            {
                return this.ProblemResult(StatusCodes.Status409Conflict, "The current cycle has not searched anything yet");
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();

            // A cycle waiting out the minimum cycle time already finished, so it keeps its reason.
            if (cycle.CompletedAt is null)
            {
                cycle.CompletedAt = now;
                cycle.CompletionReason = SeekerCycleCompletionReason.Reset;
            }

            SeekerCycle next = new()
            {
                ArrInstanceId = arrInstanceId,
                StartedAt = now,
                EligibleAtStart = cycle.EligibleNow,
                EligibleNow = cycle.EligibleNow,
                RemainingUnits = cycle.EligibleNow,
                IsDryRun = isDryRun,
            };
            await _eventsContext.SeekerCycles.AddAsync(next);

            if (!isDryRun)
            {
                instanceConfig.CurrentCycleId = next.Id;
                await _dataContext.SaveChangesAsync();
            }

            await _eventsContext.SaveChangesAsync();

            _logger.LogInformation("Cycle reset on {InstanceName} after {Count} searches",
                instanceConfig.ArrInstance.Name, cycle.SearchedUnits);

            return NoContent();
        }
        finally
        {
            _cycleLock.Release(arrInstanceId);
        }
    }
}

using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.General;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DryRun;

/// <inheritdoc/>
public sealed class DryRunPurger : IDryRunPurger
{
    private readonly ILogger<DryRunPurger> _logger;
    private readonly DataContext _dataContext;
    private readonly EventsContext _eventsContext;

    public DryRunPurger(ILogger<DryRunPurger> logger, DataContext dataContext, EventsContext eventsContext)
    {
        _logger = logger;
        _dataContext = dataContext;
        _eventsContext = eventsContext;
    }

    /// <inheritdoc/>
    public async Task PurgeIfDryRunOffAsync()
    {
        GeneralConfig config = await _dataContext.GeneralConfigs
            .AsNoTracking()
            .FirstAsync();

        if (config.DryRun)
        {
            return;
        }

        await PurgeAsync();
    }

    /// <inheritdoc/>
    public async Task PurgeAsync()
    {
        await using var transaction = await _eventsContext.Database.BeginTransactionAsync();

        try
        {
            // Read before the purge deletes these strikes.
            List<Guid> dryRunItemIds = await _eventsContext.Strikes
                .Where(s => s.IsDryRun)
                .Select(s => s.DownloadItemId)
                .Distinct()
                .ToListAsync();

            int deletedStrikes = await _eventsContext.Strikes
                .Where(s => s.IsDryRun)
                .ExecuteDeleteAsync();
            int deletedEvents = await _eventsContext.Events
                .Where(e => e.IsDryRun)
                .ExecuteDeleteAsync();
            int deletedManualEvents = await _eventsContext.ManualEvents
                .Where(e => e.IsDryRun)
                .ExecuteDeleteAsync();
            int deletedItems = await _eventsContext.DownloadItems
                .Where(d => !d.Strikes.Any())
                .ExecuteDeleteAsync();

            // Only real removals set IsRemoved and IsReturning.
            int clearedFlags = await _eventsContext.DownloadItems
                .Where(d => dryRunItemIds.Contains(d.Id) && d.IsMarkedForRemoval)
                .ExecuteUpdateAsync(setter => setter
                    .SetProperty(d => d.IsMarkedForRemoval, false));

            int deletedHistory = await _eventsContext.SeekerHistory
                .Where(h => h.IsDryRun)
                .ExecuteDeleteAsync();

            if (deletedStrikes + deletedEvents + deletedManualEvents + deletedItems + deletedHistory + clearedFlags > 0)
            {
                _logger.LogWarning(
                    "Purged dry-run data: {Strikes} strikes, {Events} events, {ManualEvents} manual events, {Items} orphaned download items, {History} search history entries removed, {Flags} removal marks cleared",
                    deletedStrikes, deletedEvents, deletedManualEvents, deletedItems, deletedHistory, clearedFlags);
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

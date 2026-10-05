using Cleanuparr.Infrastructure.Features.Arr.ForceImport;
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
    private readonly DryRunActivity _dryRunActivity;

    public DryRunPurger(
        ILogger<DryRunPurger> logger,
        DataContext dataContext,
        EventsContext eventsContext,
        DryRunActivity dryRunActivity)
    {
        _logger = logger;
        _dataContext = dataContext;
        _eventsContext = eventsContext;
        _dryRunActivity = dryRunActivity;
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
        bool ran = await _dryRunActivity.RunIfIdleAsync(async () =>
        {
            await using var transaction = await _eventsContext.Database.BeginTransactionAsync();

            try
            {
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

                int deletedHistory = await _eventsContext.SeekerHistory
                    .Where(h => h.IsDryRun)
                    .ExecuteDeleteAsync();

                if (deletedStrikes + deletedEvents + deletedManualEvents + deletedItems + deletedHistory > 0)
                {
                    _logger.LogWarning(
                        "Purged dry-run data: {Strikes} strikes, {Events} events, {ManualEvents} manual events, {Items} orphaned download items, {History} search history entries removed",
                        deletedStrikes, deletedEvents, deletedManualEvents, deletedItems, deletedHistory);
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            ForceImportService.ForgetDryRun();
        });

        if (!ran)
        {
            // Skipped purges carry over if dry run turns back on.
            _logger.LogDebug("skip dry-run purge | dry runs still active");
        }
    }
}

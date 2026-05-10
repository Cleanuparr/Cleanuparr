using Cleanuparr.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Features.Testing.Controllers;

/// <summary>
/// E2E-only controller for resetting application state between tests.
/// Active only when configuration value <c>Cleanuparr:E2eMode</c> is "true".
/// All endpoints return 404 when E2E mode is disabled, so this surface is invisible
/// in normal deployments.
/// </summary>
[ApiController]
[Route("api/__test__")]
[AllowAnonymous]
public sealed class TestResetController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TestResetController> _logger;
    private readonly DataContext _dataContext;
    private readonly EventsContext _eventsContext;
    private readonly UsersContext _usersContext;

    public TestResetController(
        IConfiguration configuration,
        ILogger<TestResetController> logger,
        DataContext dataContext,
        EventsContext eventsContext,
        UsersContext usersContext)
    {
        _configuration = configuration;
        _logger = logger;
        _dataContext = dataContext;
        _eventsContext = eventsContext;
        _usersContext = usersContext;
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset()
    {
        if (!IsE2eModeEnabled())
        {
            return NotFound();
        }

        await DataContext.Lock.WaitAsync();
        try
        {
            var eventsCounts = await ClearEventsAsync();
            var dataCounts = await ClearDynamicDataAsync();

            _logger.LogWarning(
                "[E2E reset] cleared events context ({Events} events, {Strikes} strikes, {DownloadItems} items, {ManualEvents} manual events, {JobRuns} job runs) and dynamic data ({ArrInstances} arr instances, {DownloadClients} download clients, {NotificationConfigs} notification configs, {StallRules} stall rules, {SlowRules} slow rules, {SeedingRules} seeding rules)",
                eventsCounts.Events, eventsCounts.Strikes, eventsCounts.DownloadItems, eventsCounts.ManualEvents, eventsCounts.JobRuns,
                dataCounts.ArrInstances, dataCounts.DownloadClients, dataCounts.NotificationConfigs,
                dataCounts.StallRules, dataCounts.SlowRules, dataCounts.SeedingRules);

            return Ok(new { events = eventsCounts, data = dataCounts });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPost("reset/users")]
    public async Task<IActionResult> ResetUsers()
    {
        if (!IsE2eModeEnabled())
        {
            return NotFound();
        }

        await UsersContext.Lock.WaitAsync();
        try
        {
            var refreshTokens = await _usersContext.RefreshTokens.ExecuteDeleteAsync();
            var recoveryCodes = await _usersContext.RecoveryCodes.ExecuteDeleteAsync();
            var users = await _usersContext.Users.ExecuteDeleteAsync();

            _logger.LogWarning("[E2E reset] cleared users ({Users} users, {RefreshTokens} refresh tokens, {RecoveryCodes} recovery codes)", users, refreshTokens, recoveryCodes);

            return Ok(new { users, refreshTokens, recoveryCodes });
        }
        finally
        {
            UsersContext.Lock.Release();
        }
    }

    private bool IsE2eModeEnabled()
    {
        return _configuration.GetValue<bool>("Cleanuparr:E2eMode")
            || string.Equals(Environment.GetEnvironmentVariable("CLEANUPARR_E2E_MODE"), "true", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<EventsResetCounts> ClearEventsAsync()
    {
        await using var transaction = await _eventsContext.Database.BeginTransactionAsync();
        try
        {
            var searchEventData = await _eventsContext.SearchEventData.ExecuteDeleteAsync();
            var events = await _eventsContext.Events.ExecuteDeleteAsync();
            var manualEvents = await _eventsContext.ManualEvents.ExecuteDeleteAsync();
            var strikes = await _eventsContext.Strikes.ExecuteDeleteAsync();
            var downloadItems = await _eventsContext.DownloadItems.ExecuteDeleteAsync();
            var jobRuns = await _eventsContext.JobRuns.ExecuteDeleteAsync();

            await transaction.CommitAsync();

            return new EventsResetCounts
            {
                Events = events,
                ManualEvents = manualEvents,
                Strikes = strikes,
                DownloadItems = downloadItems,
                JobRuns = jobRuns,
                SearchEventData = searchEventData,
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<DataResetCounts> ClearDynamicDataAsync()
    {
        await using var transaction = await _dataContext.Database.BeginTransactionAsync();
        try
        {
            var seekerHistory = await _dataContext.SeekerHistory.ExecuteDeleteAsync();
            var customFormatScoreHistory = await _dataContext.CustomFormatScoreHistory.ExecuteDeleteAsync();
            var customFormatScoreEntries = await _dataContext.CustomFormatScoreEntries.ExecuteDeleteAsync();
            var seekerCommandTrackers = await _dataContext.SeekerCommandTrackers.ExecuteDeleteAsync();
            var searchQueue = await _dataContext.SearchQueue.ExecuteDeleteAsync();
            var seekerInstanceConfigs = await _dataContext.SeekerInstanceConfigs.ExecuteDeleteAsync();
            var arrInstances = await _dataContext.ArrInstances.ExecuteDeleteAsync();

            var stallRules = await _dataContext.StallRules.ExecuteDeleteAsync();
            var slowRules = await _dataContext.SlowRules.ExecuteDeleteAsync();

            var qbitSeedingRules = await _dataContext.QBitSeedingRules.ExecuteDeleteAsync();
            var delugeSeedingRules = await _dataContext.DelugeSeedingRules.ExecuteDeleteAsync();
            var transmissionSeedingRules = await _dataContext.TransmissionSeedingRules.ExecuteDeleteAsync();
            var uTorrentSeedingRules = await _dataContext.UTorrentSeedingRules.ExecuteDeleteAsync();
            var rTorrentSeedingRules = await _dataContext.RTorrentSeedingRules.ExecuteDeleteAsync();
            var seedingRules = qbitSeedingRules + delugeSeedingRules + transmissionSeedingRules + uTorrentSeedingRules + rTorrentSeedingRules;

            var unlinkedConfigs = await _dataContext.UnlinkedConfigs.ExecuteDeleteAsync();
            var blacklistSyncHistory = await _dataContext.BlacklistSyncHistory.ExecuteDeleteAsync();
            var downloadClients = await _dataContext.DownloadClients.ExecuteDeleteAsync();

            var notificationConfigs = await _dataContext.NotificationConfigs.ExecuteDeleteAsync();

            await transaction.CommitAsync();

            return new DataResetCounts
            {
                ArrInstances = arrInstances,
                DownloadClients = downloadClients,
                NotificationConfigs = notificationConfigs,
                StallRules = stallRules,
                SlowRules = slowRules,
                SeedingRules = seedingRules,
                UnlinkedConfigs = unlinkedConfigs,
                SeekerHistory = seekerHistory,
                CustomFormatScoreEntries = customFormatScoreEntries,
                CustomFormatScoreHistory = customFormatScoreHistory,
                SeekerCommandTrackers = seekerCommandTrackers,
                SearchQueue = searchQueue,
                SeekerInstanceConfigs = seekerInstanceConfigs,
                BlacklistSyncHistory = blacklistSyncHistory,
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private sealed record EventsResetCounts
    {
        public int Events { get; init; }
        public int ManualEvents { get; init; }
        public int Strikes { get; init; }
        public int DownloadItems { get; init; }
        public int JobRuns { get; init; }
        public int SearchEventData { get; init; }
    }

    private sealed record DataResetCounts
    {
        public int ArrInstances { get; init; }
        public int DownloadClients { get; init; }
        public int NotificationConfigs { get; init; }
        public int StallRules { get; init; }
        public int SlowRules { get; init; }
        public int SeedingRules { get; init; }
        public int UnlinkedConfigs { get; init; }
        public int SeekerHistory { get; init; }
        public int CustomFormatScoreEntries { get; init; }
        public int CustomFormatScoreHistory { get; init; }
        public int SeekerCommandTrackers { get; init; }
        public int SearchQueue { get; init; }
        public int SeekerInstanceConfigs { get; init; }
        public int BlacklistSyncHistory { get; init; }
    }
}

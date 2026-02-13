using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Cleanuparr.Persistence.Models.State;
using Cleanuparr.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Services;

public class RuleEvaluator : IRuleEvaluator
{
    private readonly IRuleManager _ruleManager;
    private readonly IStriker _striker;
    private readonly EventsContext _context;
    private readonly ILogger<RuleEvaluator> _logger;

    public RuleEvaluator(
        IRuleManager ruleManager,
        IStriker striker,
        EventsContext context,
        ILogger<RuleEvaluator> logger)
    {
        _ruleManager = ruleManager;
        _striker = striker;
        _context = context;
        _logger = logger;
    }

    public async Task<(bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient)> EvaluateStallRulesAsync(ITorrentItemWrapper torrent)
    {
        _logger.LogTrace("Evaluating stall rules | {name}", torrent.Name);

        var rule = _ruleManager.GetMatchingStallRule(torrent);

        if (rule is null)
        {
            _logger.LogTrace("skip | no stall rules matched | {name}", torrent.Name);
            return (false, DeleteReason.None, false);
        }

        _logger.LogTrace("Applying stall rule {rule} | {name}", rule.Name, torrent.Name);
        ContextProvider.Set<QueueRule>(rule);

        await ResetStalledStrikesAsync(
            torrent,
            rule.ResetStrikesOnProgress,
            rule.MinimumProgressByteSize?.Bytes
        );

        long currentDownloaded = Math.Max(0, torrent.DownloadedBytes);
        bool shouldRemove = await _striker.StrikeAndCheckLimit(
            torrent.Hash,
            torrent.Name,
            (ushort)rule.MaxStrikes,
            StrikeType.Stalled,
            currentDownloaded
        );

        if (shouldRemove)
        {
            return (true, DeleteReason.Stalled, rule.DeletePrivateTorrentsFromClient);
        }

        return (false, DeleteReason.None, false);
    }

    public async Task<(bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient)> EvaluateSlowRulesAsync(ITorrentItemWrapper torrent)
    {
        _logger.LogTrace("Evaluating slow rules | {name}", torrent.Name);

        SlowRule? rule = _ruleManager.GetMatchingSlowRule(torrent);

        if (rule is null)
        {
            _logger.LogDebug("skip | no slow rules matched | {name}", torrent.Name);
            return (false, DeleteReason.None, false);
        }

        _logger.LogTrace("Applying slow rule {rule} | {name}", rule.Name, torrent.Name);
        ContextProvider.Set<QueueRule>(rule);

        if (!string.IsNullOrWhiteSpace(rule.MinSpeed))
        {
            ByteSize minSpeed = rule.MinSpeedByteSize;
            ByteSize currentSpeed = new ByteSize(torrent.DownloadSpeed);
            if (currentSpeed.Bytes < minSpeed.Bytes)
            {
                bool shouldRemove = await _striker.StrikeAndCheckLimit(
                    torrent.Hash,
                    torrent.Name,
                    (ushort)rule.MaxStrikes,
                    StrikeType.SlowSpeed
                );

                if (shouldRemove)
                {
                    return (true, DeleteReason.SlowSpeed, rule.DeletePrivateTorrentsFromClient);
                }
            }
            else
            {
                await ResetSlowStrikesAsync(torrent, rule.ResetStrikesOnProgress, StrikeType.SlowSpeed);
            }
        }

        if (rule.MaxTimeHours > 0)
        {
            SmartTimeSpan maxTime = SmartTimeSpan.FromHours(rule.MaxTimeHours);
            SmartTimeSpan currentTime = SmartTimeSpan.FromSeconds(torrent.Eta);
            if (currentTime.Time.TotalSeconds > maxTime.Time.TotalSeconds && maxTime.Time.TotalSeconds > 0)
            {
                bool shouldRemove = await _striker.StrikeAndCheckLimit(
                    torrent.Hash,
                    torrent.Name,
                    (ushort)rule.MaxStrikes,
                    StrikeType.SlowTime
                );

                if (shouldRemove)
                {
                    return (true, DeleteReason.SlowTime, rule.DeletePrivateTorrentsFromClient);
                }
            }
            else
            {
                await ResetSlowStrikesAsync(torrent, rule.ResetStrikesOnProgress, StrikeType.SlowTime);
            }
        }

        return (false, DeleteReason.None, false);
    }

    private async Task ResetStalledStrikesAsync(
        ITorrentItemWrapper torrent,
        bool resetEnabled,
        long? minimumProgressBytes
    )
    {
        if (!resetEnabled)
        {
            return;
        }

        var (hasProgress, previous, current) = await GetDownloadProgressAsync(torrent);
        if (!hasProgress)
        {
            _logger.LogTrace("No progress detected | strikes are not reset | {name}", torrent.Name);
            return;
        }

        long progressBytes = current - previous;

        if (minimumProgressBytes is > 0)
        {
            if (progressBytes < minimumProgressBytes)
            {
                _logger.LogTrace(
                    "Progress detected | strikes are not reset | progress: {progress}b | minimum: {minimum}b | {name}",
                    progressBytes,
                    minimumProgressBytes,
                    torrent.Name
                );

                return;
            }

            _logger.LogTrace(
                "Progress detected | strikes are reset | progress: {progress}b | minimum: {minimum}b | {name}",
                progressBytes,
                minimumProgressBytes,
                torrent.Name
            );
        }
        else
        {
            _logger.LogTrace(
                "Progress detected | strikes are reset | progress: {progress}b | {name}",
                progressBytes,
                torrent.Name
            );
        }

        await _striker.ResetStrikeAsync(torrent.Hash, torrent.Name, StrikeType.Stalled);
    }

    private async Task ResetSlowStrikesAsync(
        ITorrentItemWrapper torrent,
        bool resetEnabled,
        StrikeType strikeType
    )
    {
        if (!resetEnabled)
        {
            return;
        }

        await _striker.ResetStrikeAsync(torrent.Hash, torrent.Name, strikeType);
    }

    private async Task<(bool HasProgress, long PreviousDownloaded, long CurrentDownloaded)> GetDownloadProgressAsync(ITorrentItemWrapper torrent)
    {
        long currentDownloaded = Math.Max(0, torrent.DownloadedBytes);

        var downloadItem = await _context.DownloadItems
            .FirstOrDefaultAsync(d => d.DownloadId == torrent.Hash);

        if (downloadItem is null)
        {
            return (false, 0, currentDownloaded);
        }

        // Get the most recent strike for this download item (Stalled type) to check progress
        var mostRecentStrike = await _context.Strikes
            .Where(s => s.DownloadItemId == downloadItem.Id && s.Type == StrikeType.Stalled)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (mostRecentStrike is null)
        {
            return (false, 0, currentDownloaded);
        }

        long previousDownloaded = mostRecentStrike.LastDownloadedBytes ?? 0;
        bool progressed = currentDownloaded > previousDownloaded;

        return (progressed, previousDownloaded, currentDownloaded);
    }
}

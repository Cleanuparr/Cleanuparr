using System.Collections.Concurrent;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.State;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.ItemStriker;

public sealed class Striker : IStriker
{
    private readonly ILogger<Striker> _logger;
    private readonly EventsContext _context;
    private readonly IEventPublisher _eventPublisher;

    public static readonly ConcurrentDictionary<string, string?> RecurringHashes = [];

    public Striker(ILogger<Striker> logger, EventsContext context, IEventPublisher eventPublisher)
    {
        _logger = logger;
        _context = context;
        _eventPublisher = eventPublisher;
    }

    public async Task<bool> StrikeAndCheckLimit(string hash, string itemName, ushort maxStrikes, StrikeType strikeType, long? lastDownloadedBytes = null)
    {
        if (maxStrikes is 0)
        {
            _logger.LogTrace("skip striking for {reason} | max strikes is 0 | {name}", strikeType, itemName);
            return false;
        }

        var downloadItem = await GetOrCreateDownloadItemAsync(hash, itemName);

        int existingStrikeCount = await _context.Strikes
            .CountAsync(s => s.DownloadItemId == downloadItem.Id && s.Type == strikeType);

        var strike = new Strike
        {
            DownloadItemId = downloadItem.Id,
            JobRunId = ContextProvider.GetJobRunId(),
            Type = strikeType,
            LastDownloadedBytes = lastDownloadedBytes
        };
        _context.Strikes.Add(strike);

        int strikeCount = existingStrikeCount + 1;

        // If item was previously removed and gets a new strike, it has returned
        if (downloadItem.IsRemoved)
        {
            downloadItem.IsReturning = true;
            downloadItem.IsRemoved = false;
            downloadItem.IsMarkedForRemoval = false;
        }

        // Mark for removal when strike limit reached
        if (strikeCount >= maxStrikes)
        {
            downloadItem.IsMarkedForRemoval = true;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Item on strike number {strike} | reason {reason} | {name}", strikeCount, strikeType.ToString(), itemName);

        await _eventPublisher.PublishStrike(strikeType, strikeCount, hash, itemName, strike.Id);

        if (strikeCount < maxStrikes)
        {
            return false;
        }

        if (strikeCount > maxStrikes)
        {
            _logger.LogWarning("Blocked item keeps coming back | {name}", itemName);

            RecurringHashes.TryAdd(hash.ToLowerInvariant(), null);
            await _eventPublisher.PublishRecurringItem(hash, itemName, strikeCount);
        }

        _logger.LogInformation("Removing item with max strikes | reason {reason} | {name}", strikeType.ToString(), itemName);

        return true;
    }

    public async Task ResetStrikeAsync(string hash, string itemName, StrikeType strikeType)
    {
        var downloadItem = await _context.DownloadItems
            .FirstOrDefaultAsync(d => d.DownloadId == hash);

        if (downloadItem is null)
        {
            return;
        }

        var strikesToDelete = await _context.Strikes
            .Where(s => s.DownloadItemId == downloadItem.Id && s.Type == strikeType)
            .ToListAsync();

        if (strikesToDelete.Count > 0)
        {
            _context.Strikes.RemoveRange(strikesToDelete);
            await _context.SaveChangesAsync();
            _logger.LogTrace("Progress detected | resetting {reason} strikes from {strikeCount} to 0 | {name}", strikeType, strikesToDelete.Count, itemName);
        }
    }

    private async Task<DownloadItem> GetOrCreateDownloadItemAsync(string hash, string itemName)
    {
        var downloadItem = await _context.DownloadItems
            .FirstOrDefaultAsync(d => d.DownloadId == hash);

        if (downloadItem is not null)
        {
            if (downloadItem.Title != itemName)
            {
                downloadItem.Title = itemName;
                await _context.SaveChangesAsync();
            }
            return downloadItem;
        }

        downloadItem = new DownloadItem
        {
            DownloadId = hash,
            Title = itemName
        };
        _context.DownloadItems.Add(downloadItem);
        await _context.SaveChangesAsync();

        return downloadItem;
    }
}

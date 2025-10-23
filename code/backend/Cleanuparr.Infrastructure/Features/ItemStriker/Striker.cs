using System.Collections.Concurrent;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.ItemStriker;

public sealed class Striker : IStriker
{
    private readonly ILogger<Striker> _logger;
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _cacheOptions;
    private readonly EventPublisher _eventPublisher;

    public static readonly ConcurrentDictionary<string, string?> RecurringHashes = [];

    public Striker(ILogger<Striker> logger, IMemoryCache cache, EventPublisher eventPublisher)
    {
        _logger = logger;
        _cache = cache;
        _eventPublisher = eventPublisher;
        _cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(StaticConfiguration.TriggerValue + Constants.CacheLimitBuffer);
    }
    
    /// <inheritdoc/>
    public async Task<bool> StrikeAndCheckLimit(string hash, string itemName, ushort maxStrikes, StrikeType strikeType)
    {
        if (maxStrikes is 0)
        {
            _logger.LogTrace("skip striking for {reason} | max strikes is 0 | {name}", strikeType, itemName);
            return false;
        }
        
        string key = CacheKeys.Strike(strikeType, hash);
        
        if (!_cache.TryGetValue(key, out int strikeCount))
        {
            strikeCount = 1;
        }
        else
        {
            ++strikeCount;
        }
        
        _logger.LogInformation("Item on strike number {strike} | reason {reason} | {name}", strikeCount, strikeType.ToString(), itemName);

        await _eventPublisher.PublishStrike(strikeType, strikeCount, hash, itemName);
        
        _cache.Set(key, strikeCount, _cacheOptions);
        
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

    public Task ResetStrikeAsync(string hash, string itemName, StrikeType strikeType)
    {
        string key = CacheKeys.Strike(strikeType, hash);

        if (_cache.TryGetValue(key, out int strikeCount) && strikeCount > 0)
        {
            _logger.LogTrace("Progress detected | resetting {reason} strikes from {strikeCount} to 0 | {name}", strikeType, strikeCount, itemName);
        }

        _cache.Remove(key);

        return Task.CompletedTask;
    }
}
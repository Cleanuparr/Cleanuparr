using Cleanuparr.Domain.Entities;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Services;

public class RuleManager : IRuleManager
{
    private readonly ILogger<RuleManager> _logger;
    
    public RuleManager(ILogger<RuleManager> logger)
    {
        _logger = logger;
    }
    
    public StallRule? GetMatchingStallRule(ITorrentItem torrent)
    {
        var stallRules = ContextProvider.Get<List<StallRule>>(nameof(StallRule));
        return GetMatchingQueueRule(torrent, stallRules);
    }

    public SlowRule? GetMatchingSlowRule(ITorrentItem torrent)
    {
        var slowRules = ContextProvider.Get<List<SlowRule>>(nameof(SlowRule));
        return GetMatchingQueueRule(torrent, slowRules);
    }

    private TRule? GetMatchingQueueRule<TRule>(ITorrentItem torrent, IReadOnlyList<TRule> rules) where TRule : QueueRule
    {
        if (rules.Count is 0)
        {
            return null;
        }

        List<TRule> matchedRules = rules
            .Where(x => x.Enabled && x.MatchesTorrent(torrent))
            .ToList();

        if (matchedRules.Count > 1)
        {
            _logger.LogWarning("skip | multiple {type} rules matched | {name}", typeof(TRule).Name, torrent.Name);
            return null;
        }

        return matchedRules.FirstOrDefault();
    }
}
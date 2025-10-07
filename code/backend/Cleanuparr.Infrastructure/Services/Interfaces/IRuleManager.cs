using Cleanuparr.Domain.Entities;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

namespace Cleanuparr.Infrastructure.Services.Interfaces;

public interface IRuleManager
{
    Task<IReadOnlyList<StallRule>> GetActiveStallRulesAsync();
    Task<IReadOnlyList<SlowRule>> GetActiveSlowRulesAsync();
    StallRule? GetMatchingStallRuleAsync(ITorrentInfo torrent);
    SlowRule? GetMatchingSlowRuleAsync(ITorrentInfo torrent);
}
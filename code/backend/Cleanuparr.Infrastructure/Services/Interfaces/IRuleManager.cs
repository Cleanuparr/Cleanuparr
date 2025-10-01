using Cleanuparr.Domain.Entities;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

namespace Cleanuparr.Infrastructure.Services.Interfaces;

public interface IRuleManager
{
    Task<IReadOnlyList<StallRule>> GetActiveStallRulesAsync();
    Task<IReadOnlyList<SlowRule>> GetActiveSlowRulesAsync();
    Task<StallRule?> GetMatchingStallRuleAsync(ITorrentInfo torrent);
    Task<SlowRule?> GetMatchingSlowRuleAsync(ITorrentInfo torrent);
}
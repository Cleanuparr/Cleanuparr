using Cleanuparr.Domain.Entities;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

namespace Cleanuparr.Infrastructure.Services.Interfaces;

public interface IRuleManager
{
    StallRule? GetMatchingStallRuleAsync(ITorrentItem torrent);
    SlowRule? GetMatchingSlowRuleAsync(ITorrentItem torrent);
}
using Cleanuparr.Domain.Entities;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

namespace Cleanuparr.Infrastructure.Services.Interfaces;

public interface IQueueRuleManager
{
    StallRule? GetMatchingStallRule(ITorrentItemWrapper torrent);
    SlowRule? GetMatchingSlowRule(ITorrentItemWrapper torrent);
}
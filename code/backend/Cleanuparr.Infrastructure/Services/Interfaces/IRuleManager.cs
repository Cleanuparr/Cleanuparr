using Cleanuparr.Domain.Entities;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

namespace Cleanuparr.Infrastructure.Services.Interfaces;

public interface IRuleManager
{
    StallRule? GetMatchingStallRule(ITorrentItem torrent);
    SlowRule? GetMatchingSlowRule(ITorrentItem torrent);
}
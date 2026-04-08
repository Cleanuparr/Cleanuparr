using Cleanuparr.Domain.Entities;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

namespace Cleanuparr.Infrastructure.Services.Interfaces;

public interface ISeedingRuleEvaluator
{
    /// <summary>
    /// Returns the highest-priority matching seeding rule for the given torrent
    /// </summary>
    ISeedingRule? GetMatchingRule(ITorrentItemWrapper torrent, IEnumerable<ISeedingRule> rules);
}

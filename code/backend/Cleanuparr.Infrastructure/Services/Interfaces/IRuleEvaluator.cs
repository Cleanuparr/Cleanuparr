using Cleanuparr.Domain.Entities;
using Cleanuparr.Infrastructure.Features.DownloadClient;

namespace Cleanuparr.Infrastructure.Services.Interfaces;

public interface IRuleEvaluator
{
    Task<DownloadCheckResult> EvaluateStallRulesAsync(ITorrentInfo torrent);
    Task<DownloadCheckResult> EvaluateSlowRulesAsync(ITorrentInfo torrent);
}
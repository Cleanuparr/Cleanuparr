using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;

namespace Cleanuparr.Infrastructure.Services.Interfaces;

public interface IRuleEvaluator
{
    Task<bool> EvaluateStallRulesAsync(ITorrentInfo torrent);
    Task<bool> EvaluateSlowRulesAsync(ITorrentInfo torrent);
}
using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;

namespace Cleanuparr.Infrastructure.Services.Interfaces;

public interface IRuleEvaluator
{
    Task<(bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient)> EvaluateStallRulesAsync(ITorrentItem torrent);
    Task<(bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient)> EvaluateSlowRulesAsync(ITorrentItem torrent);
}
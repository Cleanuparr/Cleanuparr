using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;

namespace Cleanuparr.Infrastructure.Services.Interfaces;

public interface IQueueRuleEvaluator
{
    Task<(bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient, bool ChangeCategory)> EvaluateStallRulesAsync(ITorrentItemWrapper torrent);
    Task<(bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient, bool ChangeCategory)> EvaluateSlowRulesAsync(ITorrentItemWrapper torrent);
}
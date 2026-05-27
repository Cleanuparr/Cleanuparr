using Cleanuparr.Domain.Entities;
using Cleanuparr.Infrastructure.Features.DownloadClient;

namespace Cleanuparr.Infrastructure.Features.DownloadCleaner.Services;

/// <summary>
/// Loads and applies per-client seeding rules to clean completed downloads.
/// </summary>
public interface ISeedingRulesCleanupService
{
    /// <summary>
    /// Evaluates the seeding rules against the client's downloads and removes those that match.
    /// </summary>
    Task CleanAsync(IDownloadService downloadService, List<ITorrentItemWrapper> clientDownloads);
}

using Cleanuparr.Domain.Entities.Sabnzbd;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Sabnzbd;

public interface ISabnzbdClientWrapper
{
    /// <summary>
    /// Confirms the configured API key is accepted.
    /// </summary>
    Task ValidateApiKeyAsync();

    /// <summary>
    /// Fetches the queue, optionally filtered to a single job.
    /// </summary>
    Task<SabnzbdQueueData?> GetQueueAsync(string? nzoId = null);

    /// <summary>
    /// Fetches the full history (not SABnzbd's default short window), optionally filtered to a single job.
    /// </summary>
    Task<SabnzbdHistoryData?> GetHistoryAsync(string? nzoId = null);

    /// <summary>
    /// The incomplete-downloads folder SABnzbd is configured with.
    /// </summary>
    Task<string?> GetDownloadDirAsync();

    /// <summary>
    /// Deletes a job from the queue, optionally deleting its on-disk files too.
    /// </summary>
    Task DeleteFromQueueAsync(string nzoId, bool deleteFiles);

    /// <summary>
    /// Deletes a job from history for good (forces <c>archive=0</c>). <paramref name="deleteFiles"/> only
    /// removes files for a <b>Failed</b> job; SABnzbd ignores it for a Completed job.
    /// </summary>
    Task DeleteFromHistoryAsync(string nzoId, bool deleteFiles);
}

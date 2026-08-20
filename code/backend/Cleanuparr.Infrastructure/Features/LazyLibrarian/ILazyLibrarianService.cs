using Cleanuparr.Domain.Entities.LazyLibrarian;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Features.LazyLibrarian;

public interface ILazyLibrarianService
{
    /// <summary>
    /// The snatched rows Cleanuparr can act on.
    /// Magazines and comics are excluded because the book commands reject their ids.
    /// </summary>
    Task<IReadOnlyList<LazyLibrarianQueueItem>> GetQueueAsync(ArrInstance instance);

    /// <summary>
    /// Every snatched torrent hash, including magazines and comics.
    /// The Download Cleaner uses this to leave anything LazyLibrarian owns alone.
    /// </summary>
    Task<IReadOnlyList<string>> GetClaimedHashesAsync(ArrInstance instance);

    /// <summary>
    /// Asks LazyLibrarian to poll the client for a download it snatched.
    /// It marks the row aborted when the client no longer holds it.
    /// </summary>
    Task<LazyLibrarianDownloadProgress?> GetDownloadProgressAsync(ArrInstance instance, LazyLibrarianQueueItem item);

    /// <summary>
    /// Sets the book back to wanted. The audio status is separate from the ebook status.
    /// </summary>
    Task ResetItemAsync(ArrInstance instance, LazyLibrarianQueueItem item);

    Task TriggerSearchAsync(ArrInstance instance, LazyLibrarianQueueItem item);

    Task HealthCheckAsync(ArrInstance instance);
}

using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Features.Arr.ForceImport;

/// <summary>
/// Force import for downloads an arr blocked for a reason that is safe to force past.
/// </summary>
public interface IForceImportService
{
    /// <summary>
    /// Asks an arr to import a download it blocked for a reason that is safe to force past.
    /// </summary>
    Task<ForceImportOutcome> TryImportAsync(IArrClient arrClient, ArrInstance instance, QueueRecord record);

    /// <summary>
    /// Reports the imports the arr recorded for the downloads it dropped from its queue.
    /// </summary>
    /// <param name="queuedDownloadIds">Every download the arr still holds, across every page.</param>
    Task ReconcileAsync(IArrClient arrClient, ArrInstance instance, IReadOnlySet<string> queuedDownloadIds);

    /// <summary>
    /// Drops a pending import, so the download leaving the queue is never read as an import.
    /// </summary>
    void Forget(ArrInstance instance, string downloadId);
}

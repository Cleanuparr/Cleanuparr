using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Features.LazyLibrarian;

public interface ILazyLibrarianEvaluator
{
    /// <summary>
    /// The service key of the Queue Cleaner's evaluator.
    /// </summary>
    const string QueueCleanerKey = "queue-cleaner";

    /// <summary>
    /// The service key of the Malware Blocker's evaluator.
    /// </summary>
    const string MalwareBlockerKey = "malware-blocker";

    Task<IReadOnlyList<LazyLibrarianRemovalDecision>> EvaluateAsync(
        ArrInstance instance,
        IReadOnlyList<IDownloadService> downloadServices,
        IReadOnlyList<string> ignoredDownloads
    );
}

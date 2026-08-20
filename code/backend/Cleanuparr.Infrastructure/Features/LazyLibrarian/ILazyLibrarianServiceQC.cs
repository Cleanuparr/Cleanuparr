using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Features.LazyLibrarian;

public interface ILazyLibrarianServiceQC
{
    Task<IReadOnlyList<LazyLibrarianRemovalDecision>> EvaluateAsync(
        ArrInstance instance,
        IReadOnlyList<IDownloadService> downloadServices,
        IReadOnlyList<string> ignoredDownloads
    );
}

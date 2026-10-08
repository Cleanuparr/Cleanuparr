using Cleanuparr.Domain.Entities;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.LazyLibrarian;

public sealed class LazyLibrarianServiceQC : LazyLibrarianJobEvaluator
{
    public LazyLibrarianServiceQC(ILogger<LazyLibrarianServiceQC> logger, ILazyLibrarianService lazyLibrarianService)
        : base(logger, lazyLibrarianService)
    {
    }

    protected override async Task<ClientVerdict> CheckAsync(
        IDownloadService downloadService,
        string downloadId,
        IReadOnlyList<string> ignoredDownloads
    )
    {
        if (downloadService is not IQueueCheckCapable capable)
        {
            return default;
        }

        DownloadCheckResult result = await capable.ShouldRemoveFromArrQueueAsync(downloadId, ignoredDownloads);

        return new ClientVerdict(
            result.Found,
            result.ShouldRemove,
            // LazyLibrarian has no post-import category, so a category change cannot apply.
            !result.IsPrivate || result.DeleteFromClient,
            result.DeleteReason,
            result.Item
        );
    }
}

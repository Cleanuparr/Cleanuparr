using Cleanuparr.Infrastructure.Features.DownloadClient;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.LazyLibrarian;

public sealed class LazyLibrarianServiceQC : LazyLibrarianJobEvaluator, ILazyLibrarianServiceQC
{
    public LazyLibrarianServiceQC(ILogger<LazyLibrarianServiceQC> logger, ILazyLibrarianService lazyLibrarianService)
        : base(logger, lazyLibrarianService)
    {
    }

    protected override async Task<LazyLibrarianCheck> CheckAsync(
        IDownloadService downloadService,
        string hash,
        IReadOnlyList<string> ignoredDownloads
    )
    {
        DownloadCheckResult result = await downloadService.ShouldRemoveFromArrQueueAsync(hash, ignoredDownloads);

        return new LazyLibrarianCheck
        {
            Found = result.Found,
            ShouldRemove = result.ShouldRemove,
            // LazyLibrarian has no post-import category, so a category change cannot apply.
            RemoveFromClient = !result.IsPrivate || result.DeleteFromClient,
            DeleteReason = result.DeleteReason,
            Torrent = result.Torrent,
        };
    }
}

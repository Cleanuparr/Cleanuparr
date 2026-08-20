using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.LazyLibrarian;

public sealed class LazyLibrarianServiceCB : LazyLibrarianJobEvaluator, ILazyLibrarianServiceCB
{
    public LazyLibrarianServiceCB(ILogger<LazyLibrarianServiceCB> logger, ILazyLibrarianService lazyLibrarianService)
        : base(logger, lazyLibrarianService)
    {
    }

    protected override async Task<LazyLibrarianCheck> CheckAsync(
        IDownloadService downloadService,
        string hash,
        IReadOnlyList<string> ignoredDownloads
    )
    {
        BlockFilesResult result = await downloadService.BlockUnwantedFilesAsync(hash, ignoredDownloads);
        ContentBlockerConfig config = ContextProvider.Get<ContentBlockerConfig>();

        return new LazyLibrarianCheck
        {
            Found = result.Found,
            ShouldRemove = result.ShouldRemove,
            RemoveFromClient = !result.IsPrivate || config.DeletePrivate,
            DeleteReason = result.DeleteReason,
            Torrent = result.Torrent,
        };
    }
}

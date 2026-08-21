using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.LazyLibrarian;

public sealed class LazyLibrarianServiceCB : LazyLibrarianJobEvaluator
{
    public LazyLibrarianServiceCB(ILogger<LazyLibrarianServiceCB> logger, ILazyLibrarianService lazyLibrarianService)
        : base(logger, lazyLibrarianService)
    {
    }

    protected override async Task<ClientVerdict> CheckAsync(
        IDownloadService downloadService,
        string hash,
        IReadOnlyList<string> ignoredDownloads
    )
    {
        BlockFilesResult result = await downloadService.BlockUnwantedFilesAsync(hash, ignoredDownloads);
        ContentBlockerConfig config = ContextProvider.Get<ContentBlockerConfig>();

        return new ClientVerdict(
            result.Found,
            result.ShouldRemove,
            !result.IsPrivate || config.DeletePrivate,
            result.DeleteReason,
            result.Torrent
        );
    }
}

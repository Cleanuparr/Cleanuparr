namespace Cleanuparr.Infrastructure.Features.DownloadRemover.Models;

public abstract record RemovalTarget
{
    public abstract string DownloadId { get; }

    public abstract string Title { get; }
}

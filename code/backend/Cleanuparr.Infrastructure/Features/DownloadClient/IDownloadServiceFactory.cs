using Cleanuparr.Persistence.Models.Configuration;

namespace Cleanuparr.Infrastructure.Features.DownloadClient;

public interface IDownloadServiceFactory
{
    IDownloadService GetDownloadService(DownloadClientConfig downloadClientConfig);
}
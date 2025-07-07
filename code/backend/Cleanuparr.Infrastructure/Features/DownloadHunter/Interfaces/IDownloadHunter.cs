using Cleanuparr.Infrastructure.Features.DownloadHunter.Models;
using Data.Models.Arr;

namespace Cleanuparr.Infrastructure.Features.DownloadHunter.Interfaces;

public interface IDownloadHunter
{
    Task HuntDownloadsAsync<T>(DownloadHuntRequest<T> request) where T : SearchItem;
}
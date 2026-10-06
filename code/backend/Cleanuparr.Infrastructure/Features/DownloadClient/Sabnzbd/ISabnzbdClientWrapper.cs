using Cleanuparr.Domain.Entities.Sabnzbd;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Sabnzbd;

public interface ISabnzbdClientWrapper
{
    Task<string?> GetVersionAsync();
    Task<SabnzbdQueueData?> GetQueueAsync(string? nzoId = null);
    Task<SabnzbdHistoryData?> GetHistoryAsync(string? nzoId = null);
    Task<IReadOnlyList<string>> GetCategoriesAsync();
    Task DeleteFromQueueAsync(string nzoId, bool deleteFiles);
    Task DeleteFromHistoryAsync(string nzoId, bool deleteFiles);
    Task PauseAsync(string nzoId);
    Task ChangeCategoryAsync(string nzoId, string category);
}

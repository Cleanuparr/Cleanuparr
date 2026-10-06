using Cleanuparr.Domain.Entities.Sabnzbd;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Sabnzbd;

public sealed class SabnzbdClientWrapper : ISabnzbdClientWrapper
{
    private readonly SabnzbdClient _client;

    public SabnzbdClientWrapper(SabnzbdClient client)
    {
        _client = client;
    }

    public Task<string?> GetVersionAsync()
        => _client.GetVersionAsync();

    public Task<SabnzbdQueueData?> GetQueueAsync(string? nzoId = null)
        => _client.GetQueueAsync(nzoId);

    public Task<SabnzbdHistoryData?> GetHistoryAsync(string? nzoId = null)
        => _client.GetHistoryAsync(nzoId);

    public Task<IReadOnlyList<string>> GetCategoriesAsync()
        => _client.GetCategoriesAsync();

    public Task DeleteFromQueueAsync(string nzoId, bool deleteFiles)
        => _client.DeleteFromQueueAsync(nzoId, deleteFiles);

    public Task DeleteFromHistoryAsync(string nzoId, bool deleteFiles)
        => _client.DeleteFromHistoryAsync(nzoId, deleteFiles);

    public Task PauseAsync(string nzoId)
        => _client.PauseAsync(nzoId);

    public Task ChangeCategoryAsync(string nzoId, string category)
        => _client.ChangeCategoryAsync(nzoId, category);
}

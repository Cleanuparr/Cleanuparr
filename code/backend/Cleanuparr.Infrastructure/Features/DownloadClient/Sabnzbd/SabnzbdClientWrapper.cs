using Cleanuparr.Domain.Entities.Sabnzbd;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Sabnzbd;

public sealed class SabnzbdClientWrapper : ISabnzbdClientWrapper
{
    private readonly SabnzbdClient _client;

    public SabnzbdClientWrapper(SabnzbdClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public Task ValidateApiKeyAsync()
        => _client.ValidateApiKeyAsync();

    /// <inheritdoc/>
    public Task<SabnzbdQueueData?> GetQueueAsync(string? nzoId = null)
        => _client.GetQueueAsync(nzoId);

    /// <inheritdoc/>
    public Task<SabnzbdHistoryData?> GetHistoryAsync(string? nzoId = null)
        => _client.GetHistoryAsync(nzoId);

    /// <inheritdoc/>
    public Task<string?> GetDownloadDirAsync()
        => _client.GetDownloadDirAsync();

    /// <inheritdoc/>
    public Task<string?> GetCompleteDirAsync()
        => _client.GetCompleteDirAsync();

    /// <inheritdoc/>
    public Task DeleteFromQueueAsync(string nzoId, bool deleteFiles)
        => _client.DeleteFromQueueAsync(nzoId, deleteFiles);

    /// <inheritdoc/>
    public Task DeleteFromHistoryAsync(string nzoId, bool deleteFiles)
        => _client.DeleteFromHistoryAsync(nzoId, deleteFiles);
}

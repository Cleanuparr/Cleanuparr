using Common.Configuration.DownloadClient;
using Domain.Models.Deluge.Response;
using Infrastructure.Verticals.ContentBlocker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Verticals.DownloadClient.Deluge;

public sealed class DelugeService : DownloadServiceBase
{
    private readonly DelugeClient _client;
    
    public DelugeService(
        ILogger<DelugeService> logger,
        IOptions<DelugeConfig> config,
        IHttpClientFactory httpClientFactory,
        FilenameEvaluator filenameEvaluator,
        IMemoryCache cache
    ) : base(logger, filenameEvaluator, cache)
    {
        config.Value.Validate();
        _client = new (config, httpClientFactory);
    }
    
    public override async Task LoginAsync()
    {
        await _client.LoginAsync();
    }

    public override async Task<bool> ShouldRemoveFromArrQueueAsync(string hash, ushort maxStrikes)
    {
        hash = hash.ToLowerInvariant();
        
        DelugeContents? contents = null;

        TorrentStatus? status = await GetTorrentStatus(hash);
        
        if (status?.Hash is null)
        {
            _logger.LogDebug("failed to find torrent {hash} in the download client", hash);
            return false;
        }

        try
        {
            contents = await _client.GetTorrentFiles(hash);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "failed to find torrent {hash} in the download client", hash);
        }

        bool shouldRemove = contents?.Contents?.Count > 0;
        
        ProcessFiles(contents.Contents, (_, file) =>
        {
            if (file.Priority > 0)
            {
                shouldRemove = false;
            }
        });

        return shouldRemove || IsItemStuckAndShouldRemove(status, maxStrikes);
    }

    public override async Task BlockUnwantedFilesAsync(string hash)
    {
        hash = hash.ToLowerInvariant();

        TorrentStatus? status = await GetTorrentStatus(hash);
        
        if (status?.Hash is null)
        {
            _logger.LogDebug("failed to find torrent {hash} in the download client", hash);
            return;
        }
        
        DelugeContents? contents = null;

        try
        {
            contents = await _client.GetTorrentFiles(hash);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "failed to find torrent {hash} in the download client", hash);
        }

        if (contents is null)
        {
            return;
        }
        
        Dictionary<int, int> priorities = [];
        bool hasPriorityUpdates = false;

        ProcessFiles(contents.Contents, (name, file) =>
        {
            int priority = file.Priority;

            if (file.Priority is not 0 && !_filenameEvaluator.IsValid(name))
            {
                priority = 0;
                hasPriorityUpdates = true;
                _logger.LogInformation("unwanted file found | {file}", file.Path);
            }
            
            priorities.Add(file.Index, priority);
        });

        if (!hasPriorityUpdates)
        {
            return;
        }
        
        _logger.LogDebug("changing priorities | torrent {hash}", hash);

        List<int> sortedPriorities = priorities
            .OrderBy(x => x.Key)
            .Select(x => x.Value)
            .ToList();

        await _client.ChangeFilesPriority(hash, sortedPriorities);
    }
    
    private bool IsItemStuckAndShouldRemove(TorrentStatus status, ushort maxStrikes)
    {
        if (status.State is not "Downloading")
        {
            return false;
        }

        // TODO check if Eta is a number type or something else
        if (status.Eta > 0)
        {
            return false;
        }

        return StrikeAndCheckLimit(status.Hash!, status.Name!, maxStrikes);
    }

    private async Task<TorrentStatus?> GetTorrentStatus(string hash)
    {
        return await _client.SendRequest<TorrentStatus?>(
            "web.get_torrent_status",
            hash,
            new[] { "hash", "state", "name", "eta" }
        );
    }
    
    private static void ProcessFiles(Dictionary<string, DelugeFileOrDirectory> contents, Action<string, DelugeFileOrDirectory> processFile)
    {
        foreach (var (name, data) in contents)
        {
            switch (data.Type)
            {
                case "file":
                    processFile(name, data);
                    break;
                case "dir" when data.Contents is not null:
                    // Recurse into subdirectories
                    ProcessFiles(data.Contents, processFile);
                    break;
            }
        }
    }

    public override void Dispose()
    {
    }
}
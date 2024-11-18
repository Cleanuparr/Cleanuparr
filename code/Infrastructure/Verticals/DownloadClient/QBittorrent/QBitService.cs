﻿using Common.Configuration;
using Infrastructure.Verticals.ContentBlocker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QBittorrent.Client;

namespace Infrastructure.Verticals.DownloadClient.QBittorrent;

public sealed class QBitService : IDownloadService
{
    private readonly ILogger<QBitService> _logger;
    private readonly QBitConfig _config;
    private readonly QBittorrentClient _client;
    private readonly FilenameEvaluator _filenameEvaluator;

    public QBitService(
        ILogger<QBitService> logger,
        IOptions<QBitConfig> config,
        FilenameEvaluator filenameEvaluator
    )
    {
        _logger = logger;
        _config = config.Value;
        _client = new(_config.Url);
        _filenameEvaluator = filenameEvaluator;
    }

    public async Task LoginAsync()
    {
        await _client.LoginAsync(_config.Username, _config.Password);
    }

    public async Task<bool> ShouldRemoveFromArrQueueAsync(string hash)
    {
        TorrentInfo? torrent = (await _client.GetTorrentListAsync(new TorrentListQuery { Hashes = [hash] }))
            .FirstOrDefault();

        if (torrent is null)
        {
            return false;
        }

        // if all files were blocked by qBittorrent
        if (torrent is { CompletionOn: not null, Downloaded: null or 0 })
        {
            return true;
        }

        IReadOnlyList<TorrentContent>? files = await _client.GetTorrentContentsAsync(hash);

        if (files is null)
        {
            return false;
        }
        
        // if all files are marked as skip
        if (files.All(x => x.Priority is TorrentContentPriority.Skip))
        {
            return true;
        }

        return false;
    }

    public async Task BlockUnwantedFilesAsync(string hash)
    {
        IReadOnlyList<TorrentContent>? files = await _client.GetTorrentContentsAsync(hash);

        if (files is null)
        {
            return;
        }
        
        foreach (TorrentContent file in files)
        {
            if (!file.Index.HasValue)
            {
                continue;
            }

            if (file.Priority is TorrentContentPriority.Skip || _filenameEvaluator.IsValid(file.Name))
            {
                continue;
            }
            
            _logger.LogDebug("unwanted file found | {file}", file.Name);
            await _client.SetFilePriorityAsync(hash, file.Index.Value, TorrentContentPriority.Skip);
        }
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
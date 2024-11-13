﻿using Common.Configuration;
using Domain.Arr.Enums;
using Domain.Arr.Queue;
using Infrastructure.Verticals.Arr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QBittorrent.Client;

namespace Infrastructure.Verticals.BlockedTorrent;

public sealed class BlockedTorrentHandler
{
    private readonly ILogger<BlockedTorrentHandler> _logger;
    private readonly QBitConfig _qBitConfig;
    private readonly SonarrConfig _sonarrConfig;
    private readonly RadarrConfig _radarrConfig;
    private readonly SonarrClient _sonarrClient;
    private readonly RadarrClient _radarrClient;
    
    public BlockedTorrentHandler(
        ILogger<BlockedTorrentHandler> logger,
        IOptions<QBitConfig> qBitConfig,
        IOptions<SonarrConfig> sonarrConfig,
        IOptions<RadarrConfig> radarrConfig,
        SonarrClient sonarrClient,
        RadarrClient radarrClient)
    {
        _logger = logger;
        _qBitConfig = qBitConfig.Value;
        _sonarrConfig = sonarrConfig.Value;
        _radarrConfig = radarrConfig.Value;
        _sonarrClient = sonarrClient;
        _radarrClient = radarrClient;
    }
    
    public async Task HandleAsync()
    {
        QBittorrentClient qBitClient = new(_qBitConfig.Url);
        
        await qBitClient.LoginAsync(_qBitConfig.Username, _qBitConfig.Password);

        foreach (ArrInstance arrInstance in _sonarrConfig.Instances)
        {
            await ProcessInstanceAsync(qBitClient, arrInstance, InstanceType.Sonarr);
        }

        foreach (ArrInstance arrInstance in _radarrConfig.Instances)
        {
            await ProcessInstanceAsync(qBitClient, arrInstance, InstanceType.Radarr);
        }
    }

    private async Task ProcessInstanceAsync(QBittorrentClient qBitClient, ArrInstance instance, InstanceType instanceType)
    {
        ushort page = 1;
        int totalRecords = 0;
        int processedRecords = 0;
        HashSet<int> itemsToBeRefreshed = [];
        ArrClient arrClient = GetClient(instanceType);

        do
        {
            QueueListResponse queueResponse = await arrClient.GetQueueItemsAsync(instance, page);
            
            if (totalRecords is 0)
            {
                totalRecords = queueResponse.TotalRecords;
                
                _logger.LogInformation(
                    "{items} items found in queue | {url}",
                    queueResponse.TotalRecords, instance.Url);
            }
            
            foreach (QueueRecord record in queueResponse.Records)
            {
                if (record.Protocol is not "torrent")
                {
                    continue;
                }
                
                TorrentInfo? torrent = (await qBitClient.GetTorrentListAsync(new TorrentListQuery { Hashes = [record.DownloadId] }))
                    .FirstOrDefault();

                if (torrent is not { CompletionOn: not null, Downloaded: null or 0 })
                {
                    _logger.LogInformation("skip | {torrent}", record.Title);
                    return;
                }

                itemsToBeRefreshed.Add(record.SeriesId);

                await arrClient.DeleteQueueItemAsync(instance, record);
            }
            
            if (queueResponse.Records.Count is 0)
            {
                break;
            }

            processedRecords += queueResponse.Records.Count;

            if (processedRecords >= totalRecords)
            {
                break;
            }

            page++;
        } while (processedRecords < totalRecords);
        
        await arrClient.RefreshItemsAsync(instance, itemsToBeRefreshed);
    }

    private ArrClient GetClient(InstanceType type) =>
        type switch
        {
            InstanceType.Sonarr => _sonarrClient,
            InstanceType.Radarr => _radarrClient,
            _ => throw new NotImplementedException($"instance type {type} is not supported")
        };
}
﻿using Common.Configuration.Arr;
using Domain.Arr.Queue;
using Domain.Enums;
using Domain.Models.Arr;
using Infrastructure.Verticals.Arr;
using Infrastructure.Verticals.DownloadClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Infrastructure.Verticals.Jobs;

public abstract class GenericHandler : IDisposable
{
    protected readonly ILogger<GenericHandler> _logger;
    protected readonly SonarrConfig _sonarrConfig;
    protected readonly RadarrConfig _radarrConfig;
    protected readonly SonarrClient _sonarrClient;
    protected readonly RadarrClient _radarrClient;
    protected readonly ArrQueueIterator _arrArrQueueIterator;
    protected readonly IDownloadService _downloadService;

    protected GenericHandler(
        ILogger<GenericHandler> logger,
        SonarrConfig sonarrConfig,
        RadarrConfig radarrConfig,
        SonarrClient sonarrClient,
        RadarrClient radarrClient,
        ArrQueueIterator arrArrQueueIterator,
        DownloadServiceFactory downloadServiceFactory
    )
    {
        _logger = logger;
        _sonarrConfig = sonarrConfig;
        _radarrConfig = radarrConfig;
        _sonarrClient = sonarrClient;
        _radarrClient = radarrClient;
        _arrArrQueueIterator = arrArrQueueIterator;
        _downloadService = downloadServiceFactory.CreateDownloadClient();
    }

    public virtual async Task ExecuteAsync()
    {
        await _downloadService.LoginAsync();

        await ProcessArrConfigAsync(_sonarrConfig, InstanceType.Sonarr);
        await ProcessArrConfigAsync(_radarrConfig, InstanceType.Radarr);
    }

    public virtual void Dispose()
    {
        _downloadService.Dispose();
    }

    protected abstract Task ProcessInstanceAsync(ArrInstance instance, InstanceType instanceType);
    
    private async Task ProcessArrConfigAsync(ArrConfig config, InstanceType instanceType)
    {
        if (!config.Enabled)
        {
            return;
        }

        foreach (ArrInstance arrInstance in config.Instances)
        {
            try
            {
                await ProcessInstanceAsync(arrInstance, instanceType);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "failed to clean {type} instance | {url}", instanceType, arrInstance.Url);
            }
        }
    }
    
    protected ArrClient GetClient(InstanceType type) =>
        type switch
        {
            InstanceType.Sonarr => _sonarrClient,
            InstanceType.Radarr => _radarrClient,
            _ => throw new NotImplementedException($"instance type {type} is not yet supported")
        };

    protected ArrConfig GetConfig(InstanceType type) =>
        type switch
        {
            InstanceType.Sonarr => _sonarrConfig,
            InstanceType.Radarr => _radarrConfig,
            _ => throw new NotImplementedException($"instance type {type} is not yet supported")
        };
    
    protected SearchItem GetRecordSearchItem(InstanceType type, QueueRecord record)
    {
        if (InstanceType.Sonarr == type && record.EpisodeId == 0)
        {
            _logger.LogDebug(JsonConvert.SerializeObject(record));
        }
        
        return type switch
        {
            InstanceType.Sonarr when _sonarrConfig.SearchType is SonarrSearchType.Episode => new SonarrSearchItem
            {
                Id = record.EpisodeId,
                SeriesId = record.SeriesId
            },
            InstanceType.Sonarr when _sonarrConfig.SearchType is SonarrSearchType.Season => new SonarrSearchItem
            {
                Id = record.SeasonNumber,
                SeriesId = record.SeriesId
            },
            InstanceType.Sonarr when _sonarrConfig.SearchType is SonarrSearchType.Series => new SonarrSearchItem
            {
                Id = record.SeriesId,
            },
            InstanceType.Radarr => new SearchItem
            {
                Id = record.MovieId,
            },
            _ => throw new NotImplementedException($"instance type {type} is not yet supported")
        };
    }
}
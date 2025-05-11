﻿using Domain.Models.Arr;
using Infrastructure.Verticals.DownloadRemover.Interfaces;
using Infrastructure.Verticals.DownloadRemover.Models;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Verticals.DownloadRemover.Consumers;

public class DownloadRemoverConsumer<T> : IConsumer<QueueItemRemoveRequest<T>>
    where T : SearchItem
{
    private readonly ILogger<DownloadRemoverConsumer<T>> _logger;
    private readonly IQueueItemRemover _queueItemRemover;

    public DownloadRemoverConsumer(
        ILogger<DownloadRemoverConsumer<T>> logger,
        IQueueItemRemover queueItemRemover
    )
    {
        _logger = logger;
        _queueItemRemover = queueItemRemover;
    }

    public async Task Consume(ConsumeContext<QueueItemRemoveRequest<T>> context)
    {
        try
        {
            await _queueItemRemover.RemoveQueueItemAsync(context.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "failed to remove queue item| {title} | {url}",
                context.Message.Record.Title,
                context.Message.Instance.Url
            );
        }
    }
}
using Cleanuparr.Infrastructure.Features.DownloadRemover.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadRemover.Consumers;

public sealed class DownloadRemoverConsumer : IConsumer<QueueItemRemoveRequest>
{
    private readonly ILogger<DownloadRemoverConsumer> _logger;
    private readonly IQueueItemRemover _queueItemRemover;

    public DownloadRemoverConsumer(
        ILogger<DownloadRemoverConsumer> logger,
        IQueueItemRemover queueItemRemover
    )
    {
        _logger = logger;
        _queueItemRemover = queueItemRemover;
    }

    public async Task Consume(ConsumeContext<QueueItemRemoveRequest> context)
    {
        try
        {
            await _queueItemRemover.RemoveQueueItemAsync(context.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "failed to remove queue item | {title} | {url}",
                context.Message.Target.Title,
                context.Message.Instance.Url
            );
        }
    }
}

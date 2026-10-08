using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;
using Cleanuparr.Infrastructure.Features.DryRun;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadRemover.Consumers;

public sealed class DownloadRemoverConsumer : IConsumer<QueueItemRemoveRequest>
{
    private readonly ILogger<DownloadRemoverConsumer> _logger;
    private readonly IQueueItemRemover _queueItemRemover;
    private readonly DryRunActivity _dryRunActivity;

    public DownloadRemoverConsumer(
        ILogger<DownloadRemoverConsumer> logger,
        IQueueItemRemover queueItemRemover,
        DryRunActivity dryRunActivity
    )
    {
        _logger = logger;
        _queueItemRemover = queueItemRemover;
        _dryRunActivity = dryRunActivity;
    }

    public async Task Consume(ConsumeContext<QueueItemRemoveRequest> context)
    {
        bool trackedDryRun = false;

        try
        {
            ContextProvider.SetDryRun(context.Message.IsDryRun);

            if (context.Message.IsDryRun)
            {
                await _dryRunActivity.EnterAsync();
                trackedDryRun = true;
            }

            try
            {
                await _queueItemRemover.RemoveQueueItemAsync(context.Message);
            }
            finally
            {
                if (trackedDryRun)
                {
                    _dryRunActivity.Exit();
                }
            }
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

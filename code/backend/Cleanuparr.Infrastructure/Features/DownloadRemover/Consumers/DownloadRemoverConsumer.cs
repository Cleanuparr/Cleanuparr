using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;
using Cleanuparr.Infrastructure.Features.DryRun;
using Cleanuparr.Infrastructure.Features.Messaging;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadRemover.Consumers;

/// <summary>
/// Removes one queued download removal request.
/// </summary>
public sealed class DownloadRemoverConsumer : IMessageHandler<QueueItemRemoveRequest>
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

    /// <inheritdoc />
    public async Task HandleAsync(QueueItemRemoveRequest message)
    {
        bool trackedDryRun = false;

        try
        {
            ContextProvider.SetDryRun(message.IsDryRun);

            if (message.IsDryRun)
            {
                await _dryRunActivity.EnterAsync();
                trackedDryRun = true;
            }

            try
            {
                await _queueItemRemover.RemoveQueueItemAsync(message);
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
                "failed to remove queue item | {Title} | {Url}",
                message.Target.Title,
                message.Instance.Url
            );
        }
    }
}

using Cleanuparr.Infrastructure.Features.DownloadHunter.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadHunter.Models;
using Data.Models.Arr;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadHunter.Consumers;

public class DownloadHunterConsumer<T> : IConsumer<DownloadHuntRequest<T>>
    where T : SearchItem
{
    private readonly ILogger<DownloadHunterConsumer<T>> _logger;
    private readonly IDownloadHunter _downloadHunter;

    public DownloadHunterConsumer(ILogger<DownloadHunterConsumer<T>> logger, IDownloadHunter downloadHunter)
    {
        _logger = logger;
        _downloadHunter = downloadHunter;
    }
    
    public async Task Consume(ConsumeContext<DownloadHuntRequest<T>> context)
    {
        try
        {
            await _downloadHunter.HuntDownloadsAsync(context.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "failed to search for replacement | {title} | {url}",
                context.Message.Record.Title,
                context.Message.Instance.Url
            );
        }
    }
}
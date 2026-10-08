using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Messaging;

/// <summary>
/// Drains a channel one message at a time, each in its own DI scope.
/// </summary>
/// <typeparam name="TMessage">The queued message type.</typeparam>
public sealed class QueueWorker<TMessage> : BackgroundService
{
    private readonly ILogger<QueueWorker<TMessage>> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Channel<TMessage> _channel;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueWorker{TMessage}"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="scopeFactory">Creates a DI scope per message.</param>
    /// <param name="channel">The channel to drain.</param>
    public QueueWorker(
        ILogger<QueueWorker<TMessage>> logger,
        IServiceScopeFactory scopeFactory,
        Channel<TMessage> channel)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _channel = channel;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (TMessage message in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await HandleAsync(message);
        }
    }

    // A separate async method keeps each message's ContextProvider values out of the next one.
    private async Task HandleAsync(TMessage message)
    {
        try
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            IMessageHandler<TMessage> handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<TMessage>>();
            await handler.HandleAsync(message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "failed to handle queued message | {MessageType}", typeof(TMessage).Name);
        }
    }
}

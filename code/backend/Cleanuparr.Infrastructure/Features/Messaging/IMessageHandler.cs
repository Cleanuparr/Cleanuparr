namespace Cleanuparr.Infrastructure.Features.Messaging;

/// <summary>
/// Handles one message taken from a <see cref="QueueWorker{TMessage}"/> queue.
/// </summary>
/// <typeparam name="TMessage">The queued message type.</typeparam>
public interface IMessageHandler<in TMessage>
{
    /// <summary>
    /// Handles a single queued message.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    Task HandleAsync(TMessage message);
}

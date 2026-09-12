using Cleanuparr.Infrastructure.Logging;

namespace Cleanuparr.Infrastructure.Realtime;

/// <summary>
/// Pushes log entries to connected clients.
/// </summary>
public interface ILogNotifier
{
    /// <summary>
    /// Pushes a log entry without waiting for delivery.
    /// </summary>
    void NotifyLog(LogEntry entry);
}

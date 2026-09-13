using System.Collections.Concurrent;
using System.Globalization;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Infrastructure.Realtime;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace Cleanuparr.Infrastructure.Logging;

/// <summary>
/// A Serilog sink that buffers log events and pushes them to connected clients
/// </summary>
public class RealtimeLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEntry> _logBuffer;
    private readonly int _bufferSize;
    private readonly MessageTemplateTextFormatter _formatter = new("{Message:l}", CultureInfo.InvariantCulture);
    private ILogNotifier? _notifier;

    public static RealtimeLogSink Instance { get; } = new();

    private RealtimeLogSink()
    {
        _bufferSize = 100;
        _logBuffer = new ConcurrentQueue<LogEntry>();
    }

    public void SetNotifier(ILogNotifier notifier)
    {
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier), "Log notifier cannot be null");
    }

    /// <summary>
    /// Processes and emits a log event to connected clients
    /// </summary>
    /// <param name="logEvent">The log event to emit</param>
    public void Emit(LogEvent logEvent)
    {
        try
        {
            StringWriter stringWriter = new();
            _formatter.Format(logEvent, stringWriter);
            LogEntry entry = new()
            {
                Timestamp = logEvent.Timestamp,
                Level = logEvent.Level.ToString(),
                Message = stringWriter.ToString(),
                Exception = logEvent.Exception?.ToString(),
                JobName = GetPropertyValue(logEvent, LogProperties.JobName),
                Category = GetPropertyValue(logEvent, LogProperties.Category, "SYSTEM"),
                InstanceName = GetPropertyValue(logEvent, LogProperties.InstanceName),
                DownloadClientType = GetPropertyValue(logEvent, LogProperties.DownloadClientType),
                DownloadClientName = GetPropertyValue(logEvent, LogProperties.DownloadClientName),
                JobRunId = GetPropertyValue(logEvent, LogProperties.JobRunId),
            };

            // Add to buffer for new clients
            AddToBuffer(entry);

            _notifier?.NotifyLog(entry);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to send log event to connected clients");
        }
    }

    /// <summary>
    /// Gets the buffer of recent logs
    /// </summary>
    public IEnumerable<LogEntry> GetRecentLogs()
    {
        return _logBuffer.ToArray();
    }

    private void AddToBuffer(LogEntry entry)
    {
        _logBuffer.Enqueue(entry);

        // Trim buffer if it exceeds the limit
        while (_logBuffer.Count > _bufferSize && _logBuffer.TryDequeue(out _))
        {
            // Nothing to do here
        }
    }

    private static string? GetPropertyValue(LogEvent logEvent, string propertyName, string? defaultValue = null)
    {
        if (logEvent.Properties.TryGetValue(propertyName, out LogEventPropertyValue? value))
        {
            return value.ToString().Trim('\"');
        }

        return defaultValue;
    }
}

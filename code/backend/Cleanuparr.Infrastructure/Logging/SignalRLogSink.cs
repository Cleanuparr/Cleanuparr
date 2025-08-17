using System.Collections.Concurrent;
using System.Globalization;
using Cleanuparr.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace Cleanuparr.Infrastructure.Logging;

/// <summary>
/// A Serilog sink that sends log events to SignalR clients
/// </summary>
public class SignalRLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<object> _logBuffer;
    private readonly int _bufferSize;
    private readonly MessageTemplateTextFormatter _formatter = new("{Message:l}", CultureInfo.InvariantCulture);
    private IHubContext<AppHub>? _appHubContext;
    
    public static SignalRLogSink Instance { get; } = new();
    
    private SignalRLogSink()
    {
        _bufferSize = 100;
        _logBuffer = new ConcurrentQueue<object>();
    }
    
    public void SetAppHubContext(IHubContext<AppHub> appHubContext)
    {
        _appHubContext = appHubContext ?? throw new ArgumentNullException(nameof(appHubContext), "AppHub context cannot be null");
    }
    
    /// <summary>
    /// Processes and emits a log event to SignalR clients
    /// </summary>
    /// <param name="logEvent">The log event to emit</param>
    public void Emit(LogEvent logEvent)
    {
        try
        {
            StringWriter stringWriter = new();
            _formatter.Format(logEvent, stringWriter);
            var logData = new
            {
                Timestamp = logEvent.Timestamp.DateTime,
                Level = logEvent.Level.ToString(),
                Message = stringWriter.ToString(),
                Exception = logEvent.Exception?.ToString(),
                JobName = GetPropertyValue(logEvent, "JobName"),
                Category = GetPropertyValue(logEvent, "Category", "SYSTEM"),
            };
            
            // Add to buffer for new clients
            AddToBuffer(logData);
            
            // Send to connected clients via the unified hub
            if (_appHubContext is not null)
            {
                _ = _appHubContext.Clients.All.SendAsync("LogReceived", logData);
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to send log event via SignalR");
        }
    }
    
    /// <summary>
    /// Gets the buffer of recent logs
    /// </summary>
    public IEnumerable<object> GetRecentLogs()
    {
        return _logBuffer.ToArray();
    }
    
    private void AddToBuffer(object logData)
    {
        _logBuffer.Enqueue(logData);
        
        // Trim buffer if it exceeds the limit
        while (_logBuffer.Count > _bufferSize && _logBuffer.TryDequeue(out _)) { }
    }
    
    private static string? GetPropertyValue(LogEvent logEvent, string propertyName, string? defaultValue = null)
    {
        if (logEvent.Properties.TryGetValue(propertyName, out var value))
        {
            return value.ToString().Trim('\"');
        }
        
        return defaultValue;
    }
}

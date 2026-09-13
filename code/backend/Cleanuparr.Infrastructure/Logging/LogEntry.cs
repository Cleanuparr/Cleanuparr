namespace Cleanuparr.Infrastructure.Logging;

/// <summary>
/// A rendered log event buffered for realtime delivery.
/// </summary>
public sealed record LogEntry
{
    public required DateTimeOffset Timestamp { get; init; }

    public required string Level { get; init; }

    public required string Message { get; init; }

    public string? Exception { get; init; }

    public string? JobName { get; init; }

    public string? Category { get; init; }

    public string? InstanceName { get; init; }

    public string? DownloadClientType { get; init; }

    public string? DownloadClientName { get; init; }

    public string? JobRunId { get; init; }
}

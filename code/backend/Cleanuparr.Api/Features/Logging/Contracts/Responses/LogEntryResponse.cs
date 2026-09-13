using Cleanuparr.Infrastructure.Logging;

namespace Cleanuparr.Api.Features.Logging.Contracts.Responses;

public sealed record LogEntryResponse
{
    public DateTimeOffset Timestamp { get; init; }

    public required string Level { get; init; }

    public required string Message { get; init; }

    public string? Exception { get; init; }

    public string? JobName { get; init; }

    public string? Category { get; init; }

    public string? InstanceName { get; init; }

    public string? DownloadClientType { get; init; }

    public string? DownloadClientName { get; init; }

    public string? JobRunId { get; init; }

    public static LogEntryResponse From(LogEntry entry) => new()
    {
        Timestamp = entry.Timestamp,
        Level = entry.Level,
        Message = entry.Message,
        Exception = entry.Exception,
        JobName = entry.JobName,
        Category = entry.Category,
        InstanceName = entry.InstanceName,
        DownloadClientType = entry.DownloadClientType,
        DownloadClientName = entry.DownloadClientName,
        JobRunId = entry.JobRunId,
    };
}

using Cleanuparr.Domain.Entities.Arr.Queue;

namespace Cleanuparr.Infrastructure.Features.Arr.ForceImport;

/// <summary>
/// An import the arr was asked for and has not answered yet.
/// </summary>
/// <param name="Record">The queue item, kept so the notification can name and picture it.</param>
/// <param name="FileCount">How many files the arr was asked to import.</param>
/// <param name="ImportedBefore">How many imports the arr had recorded for the download when it was asked.</param>
/// <param name="AskedAt">When the arr was asked, so an import that never lands is given up on.</param>
public sealed record PendingForceImport(
    QueueRecord Record,
    int FileCount,
    int ImportedBefore,
    DateTimeOffset AskedAt
);

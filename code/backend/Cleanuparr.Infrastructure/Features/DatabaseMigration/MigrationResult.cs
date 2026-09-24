namespace Cleanuparr.Infrastructure.Features.DatabaseMigration;

public sealed record MigrationResult(bool Success, string? Error, IReadOnlyDictionary<string, int> TableCounts);

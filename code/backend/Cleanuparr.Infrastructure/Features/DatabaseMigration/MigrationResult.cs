namespace Cleanuparr.Infrastructure.Features.DatabaseMigration;

/// <summary>
/// Result of a database migration operation.
/// </summary>
/// <param name="Success">
/// Whether the migration completed.
/// </param>
/// <param name="Error">
/// Error message if the migration failed.
/// </param>
/// <param name="TableCounts">
/// Row counts for each table after migration.
/// </param>
public sealed record MigrationResult(bool Success, string? Error, IReadOnlyDictionary<string, int> TableCounts);

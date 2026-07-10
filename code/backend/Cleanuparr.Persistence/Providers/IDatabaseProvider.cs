using Cleanuparr.Domain.Enums;
using Cleanuparr.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Cleanuparr.Persistence.Providers;

public interface IDatabaseProvider
{
    DatabaseProvider Kind { get; }

    void ConfigureContext(DbContextOptionsBuilder builder, DbContextKind kind);

    string? GetSchema(DbContextKind kind);

    void ConfigureConventions(ModelConfigurationBuilder builder);

    string GetUnresolvedEventFilter();

    Task OnPostMigrateAsync(DatabaseFacade database, DbContextKind kind, CancellationToken cancellationToken);

    Task PrepareBulkWriteAsync(DatabaseFacade database, CancellationToken cancellationToken);

    Task CheckpointAsync(DatabaseFacade database, CancellationToken cancellationToken);

    string EscapeLikePattern(string input);

    string GetTimelineBucketExpr(TimelineBucketSize size);

    string FormatBooleanLiteral(bool value);

    bool IsUniqueConstraintViolation(DbUpdateException exception);
}

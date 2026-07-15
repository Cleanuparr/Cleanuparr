using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cleanuparr.Infrastructure.Features.DatabaseMigration;

public sealed class ModelDataCopier
{
    private const int BatchSize = 5000;

    public async Task CopyAsync(DbContext source, DbContext target, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        IReadOnlyList<IEntityType> ordered = OrderByDependencies(target.Model.GetEntityTypes());

        bool autoDetectChanges = target.ChangeTracker.AutoDetectChangesEnabled;
        target.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            foreach (IEntityType entityType in ordered)
            {
                MethodInfo copyMethod = typeof(ModelDataCopier)
                    .GetMethod(nameof(CopyEntitySetAsync), BindingFlags.Instance | BindingFlags.NonPublic)!
                    .MakeGenericMethod(entityType.ClrType);

                int copied = await (Task<int>)copyMethod.Invoke(this, new object?[] { source, target, progress, cancellationToken })!;
                progress?.Report($"{entityType.GetTableName()}: {copied} rows");
            }
        }
        finally
        {
            target.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        }
    }

    public static IReadOnlyList<IEntityType> OrderByDependencies(IEnumerable<IEntityType> entityTypes)
    {
        List<IEntityType> types = entityTypes.Where(entityType => !entityType.IsOwned()).ToList();
        HashSet<IEntityType> visited = new();
        HashSet<IEntityType> onStack = new();
        List<IEntityType> ordered = new();

        void Visit(IEntityType type)
        {
            if (!visited.Add(type))
            {
                return;
            }

            onStack.Add(type);

            foreach (IForeignKey foreignKey in type.GetForeignKeys())
            {
                IEntityType principal = foreignKey.PrincipalEntityType;
                if (ReferenceEquals(principal, type) || !types.Contains(principal))
                {
                    continue;
                }

                if (onStack.Contains(principal))
                {
                    throw new InvalidOperationException(
                        $"Circular FK dependency between '{type.GetTableName()}' and '{principal.GetTableName()}'; copier cannot order inserts. Break the cycle or add deferred-constraint handling.");
                }

                Visit(principal);
            }

            onStack.Remove(type);
            ordered.Add(type);
        }

        foreach (IEntityType type in types)
        {
            Visit(type);
        }

        return ordered;
    }

    private async Task<int> CopyEntitySetAsync<TEntity>(DbContext source, DbContext target, IProgress<string>? progress, CancellationToken cancellationToken)
        where TEntity : class
    {
        string? table = target.Model.FindEntityType(typeof(TEntity))?.GetTableName();
        int count = 0;
        IAsyncEnumerable<TEntity> rows = source.Set<TEntity>().AsNoTracking().AsAsyncEnumerable();

        await foreach (TEntity entity in rows.WithCancellation(cancellationToken))
        {
            target.Set<TEntity>().Add(entity);
            count++;

            if (count % BatchSize == 0)
            {
                await target.SaveChangesAsync(cancellationToken);
                target.ChangeTracker.Clear();
                progress?.Report($"{table}: {count} rows copied...");
            }
        }

        if (target.ChangeTracker.Entries().Any())
        {
            await target.SaveChangesAsync(cancellationToken);
            target.ChangeTracker.Clear();
        }

        return count;
    }
}

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cleanuparr.Infrastructure.Features.DatabaseMigration;

public sealed class ModelDataCopier
{
    private const int BatchSize = 5000;

    public async Task CopyAsync(DbContext source, DbContext target, CancellationToken cancellationToken)
    {
        IReadOnlyList<IEntityType> ordered = OrderByDependencies(target.Model.GetEntityTypes());

        foreach (IEntityType entityType in ordered)
        {
            MethodInfo copyMethod = typeof(ModelDataCopier)
                .GetMethod(nameof(CopyEntitySetAsync), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(entityType.ClrType);

            await (Task)copyMethod.Invoke(this, new object[] { source, target, cancellationToken })!;
        }
    }

    public static IReadOnlyList<IEntityType> OrderByDependencies(IEnumerable<IEntityType> entityTypes)
    {
        List<IEntityType> types = entityTypes.Where(entityType => !entityType.IsOwned()).ToList();
        HashSet<IEntityType> visited = new();
        List<IEntityType> ordered = new();

        void Visit(IEntityType type)
        {
            if (!visited.Add(type))
            {
                return;
            }

            foreach (IForeignKey foreignKey in type.GetForeignKeys())
            {
                IEntityType principal = foreignKey.PrincipalEntityType;
                if (!ReferenceEquals(principal, type) && types.Contains(principal))
                {
                    Visit(principal);
                }
            }

            ordered.Add(type);
        }

        foreach (IEntityType type in types)
        {
            Visit(type);
        }

        return ordered;
    }

    private async Task CopyEntitySetAsync<TEntity>(DbContext source, DbContext target, CancellationToken cancellationToken)
        where TEntity : class
    {
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
            }
        }

        if (target.ChangeTracker.Entries().Any())
        {
            await target.SaveChangesAsync(cancellationToken);
            target.ChangeTracker.Clear();
        }
    }
}

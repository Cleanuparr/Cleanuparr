using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cleanuparr.Infrastructure.Features.DatabaseMigration;

public sealed class KeepKeysModelCustomizer : RelationalModelCustomizer
{
    public KeepKeysModelCustomizer(ModelCustomizerDependencies dependencies) : base(dependencies)
    {
    }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            IMutableKey? primaryKey = entityType.FindPrimaryKey();
            if (primaryKey is null)
            {
                continue;
            }

            foreach (IMutableProperty property in primaryKey.Properties)
            {
                property.ValueGenerated = ValueGenerated.Never;
            }
        }
    }
}

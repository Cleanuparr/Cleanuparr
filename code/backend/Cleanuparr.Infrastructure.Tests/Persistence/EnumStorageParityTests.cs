using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Persistence;

public class EnumStorageParityTests
{
    [Fact]
    public void Data_context_stores_every_enum_as_text()
    {
        List<IProperty> enumProperties = FindEnumProperties(BuildContext<DataContext>());

        enumProperties.ShouldNotBeEmpty();
        AssertStoredAsText(enumProperties);
    }

    [Fact]
    public void Events_context_stores_every_enum_as_text()
    {
        List<IProperty> enumProperties = FindEnumProperties(BuildContext<EventsContext>());

        enumProperties.ShouldNotBeEmpty();
        AssertStoredAsText(enumProperties);
    }

    private static void AssertStoredAsText(List<IProperty> enumProperties)
    {
        foreach (IProperty property in enumProperties)
        {
            string name = $"{property.DeclaringType.DisplayName()}.{property.Name}";

            property.GetValueConverter().ShouldNotBeNull($"{name} has no value converter");
            property.GetValueConverter()!.ProviderClrType.ShouldBe(
                typeof(string),
                $"{name} is not stored as text");
        }
    }

    private static List<IProperty> FindEnumProperties(DbContext context) =>
        context.Model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetDeclaredProperties())
            .Concat(context.Model.GetEntityTypes()
                .SelectMany(entityType => entityType.GetComplexProperties())
                .SelectMany(complexProperty => complexProperty.ComplexType.GetDeclaredProperties()))
            .Where(property => IsEnum(property.ClrType))
            .ToList();

    private static bool IsEnum(Type clrType) =>
        clrType.IsEnum || Nullable.GetUnderlyingType(clrType)?.IsEnum is true;

    // Only the model is read, so the file is never created.
    private static TContext BuildContext<TContext>()
        where TContext : DbContext =>
        SqliteTestDatabase.Create("model").CreateContext<TContext>();
}

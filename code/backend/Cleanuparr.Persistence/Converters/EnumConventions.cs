using System.Reflection;
using Cleanuparr.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Cleanuparr.Persistence.Converters;

public static class EnumConventions
{
    /// <summary>
    /// Stores every mapped enum property as lowercase text, complex types included.
    /// An unrecognised value reads as null, as Unknown, or as the declared default.
    /// Call last in OnModelCreating: a property that already has a converter is left alone.
    /// </summary>
    public static void ApplyLowercaseEnumConversions(this ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            Apply(entityType);
        }
    }

    private static void Apply(IMutableTypeBase type)
    {
        object? declaredDefaults = null;

        foreach (PropertyInfo property in GetEnumProperties(type.ClrType))
        {
            IMutableProperty? mapped = type.FindProperty(property.Name);
            if (mapped is null || mapped.GetValueConverter() is not null)
            {
                continue;
            }

            Type? nullableEnumType = Nullable.GetUnderlyingType(property.PropertyType);
            if (nullableEnumType is not null)
            {
                mapped.SetValueConverter(Instantiate(typeof(NullableLowercaseEnumConverter<>), nullableEnumType));
                continue;
            }

            if (HasSentinel(property.PropertyType))
            {
                mapped.SetValueConverter(Instantiate(typeof(SentinelLowercaseEnumConverter<>), property.PropertyType));
                continue;
            }

            // A fresh instance carries the property initializers.
            declaredDefaults ??= Activator.CreateInstance(type.ClrType)!;
            object fallback = property.GetValue(declaredDefaults)!;

            mapped.SetValueConverter(
                Instantiate(typeof(DefaultingLowercaseEnumConverter<>), property.PropertyType, fallback));
        }

        foreach (IMutableComplexProperty complexProperty in type.GetComplexProperties())
        {
            Apply(complexProperty.ComplexType);
        }
    }

    private static ValueConverter Instantiate(Type openConverter, Type enumType, params object?[] arguments) =>
        (ValueConverter)Activator.CreateInstance(openConverter.MakeGenericType(enumType), arguments)!;

    private static bool HasSentinel(Type enumType) =>
        Enum.GetNames(enumType).Contains(EnumSentinel.Unknown);

    private static IEnumerable<PropertyInfo> GetEnumProperties(Type clrType) =>
        clrType.GetProperties()
            .Where(p => p.PropertyType.IsEnum || Nullable.GetUnderlyingType(p.PropertyType)?.IsEnum is true);
}

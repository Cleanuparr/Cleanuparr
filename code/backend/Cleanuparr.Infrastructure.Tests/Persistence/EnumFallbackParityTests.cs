using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Serilog.Events;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Persistence;

/// <summary>
/// A settings column reads unknown text as its property initializer.
/// Omit the initializer and that becomes ordinal 0, which is nobody's decision.
/// </summary>
public class EnumFallbackParityTests
{
    private static readonly Dictionary<string, object> ExpectedFallbacks = new()
    {
        ["GeneralConfig.HttpCertificateValidation"] = CertificateValidationType.Enabled,
        ["LoggingConfig.Level"] = LogEventLevel.Information,
        ["FailedImportConfig.PatternMode"] = PatternMode.Include,
        ["BlocklistSettings.BlocklistType"] = BlocklistType.Blacklist,
        ["StallRule.PrivacyType"] = TorrentPrivacyType.Public,
        ["SlowRule.PrivacyType"] = TorrentPrivacyType.Public,
        ["QBitSeedingRule.PrivacyType"] = TorrentPrivacyType.Public,
        ["DelugeSeedingRule.PrivacyType"] = TorrentPrivacyType.Public,
        ["TransmissionSeedingRule.PrivacyType"] = TorrentPrivacyType.Public,
        ["UTorrentSeedingRule.PrivacyType"] = TorrentPrivacyType.Public,
        ["RTorrentSeedingRule.PrivacyType"] = TorrentPrivacyType.Public,
        ["SeekerConfig.SelectionStrategy"] = SelectionStrategy.BalancedWeighted,
        ["NtfyConfig.AuthenticationType"] = NtfyAuthenticationType.None,
        ["NtfyConfig.Priority"] = NtfyPriority.Default,
        ["PushoverConfig.Priority"] = PushoverPriority.Normal,
        ["AppriseConfig.Mode"] = AppriseMode.Api,
    };

    [Fact]
    public void Every_defaulting_column_falls_back_to_the_declared_value()
    {
        foreach ((string name, ValueConverter converter) in FindDefaultingProperties())
        {
            ExpectedFallbacks.ShouldContainKey(name);
            converter.ConvertFromProvider("fromthefuture").ShouldBe(ExpectedFallbacks[name], name);
        }
    }

    [Fact]
    public void No_declared_fallback_is_stale()
    {
        HashSet<string> found = FindDefaultingProperties().Select(pair => pair.Name).ToHashSet();

        found.ShouldBe(ExpectedFallbacks.Keys.ToHashSet(), ignoreOrder: true);
    }

    private static List<(string Name, ValueConverter Converter)> FindDefaultingProperties() =>
        new[] { Model<DataContext>(), Model<EventsContext>() }
            .SelectMany(AllProperties)
            .Select(property => (Property: property, Converter: property.GetValueConverter()))
            .Where(pair => IsDefaulting(pair.Converter))
            .Select(pair => ($"{pair.Property.DeclaringType.ClrType.Name}.{pair.Property.Name}", pair.Converter!))
            .Distinct()
            .ToList();

    private static bool IsDefaulting(ValueConverter? converter) =>
        converter?.GetType() is { IsGenericType: true } type
        && type.GetGenericTypeDefinition() == typeof(DefaultingLowercaseEnumConverter<>);

    private static IEnumerable<IProperty> AllProperties(IModel model) =>
        model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetDeclaredProperties())
            .Concat(model.GetEntityTypes()
                .SelectMany(entityType => entityType.GetComplexProperties())
                .SelectMany(complexProperty => complexProperty.ComplexType.GetDeclaredProperties()));

    // Only the model is read, so the file is never created.
    private static IModel Model<TContext>()
        where TContext : DbContext =>
        SqliteTestDatabase.Create("model").CreateContext<TContext>().Model;
}

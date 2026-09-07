using Cleanuparr.Domain.Enums;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Controllers;

public sealed class EnumSentinelTests
{
    [Fact]
    public void SelectableNames_omits_the_sentinel()
    {
        List<string> names = EnumSentinel.SelectableNames<EventType>();

        names.ShouldNotContain(EnumSentinel.Unknown);
        names.ShouldContain(nameof(EventType.StrikeReset));
        names.Count.ShouldBe(Enum.GetNames<EventType>().Length - 1);
    }

    [Theory]
    [InlineData(typeof(InstanceType))]
    [InlineData(typeof(DownloadClientTypeName))]
    [InlineData(typeof(DownloadClientType))]
    [InlineData(typeof(NotificationProviderType))]
    [InlineData(typeof(EventType))]
    [InlineData(typeof(EventSeverity))]
    [InlineData(typeof(ManualEventType))]
    [InlineData(typeof(StrikeType))]
    [InlineData(typeof(JobType))]
    [InlineData(typeof(SearchCommandStatus))]
    [InlineData(typeof(SeedingRuleAction))]
    public void Identity_enums_pin_the_sentinel_to_its_own_value(Type enumType)
    {
        Convert.ToInt32(Enum.Parse(enumType, EnumSentinel.Unknown))
            .ShouldBe(EnumSentinel.UnknownValue);

        // A member added later cannot take the sentinel's place.
        Enum.GetNames(enumType)[^1].ShouldBe(EnumSentinel.Unknown);
    }
}

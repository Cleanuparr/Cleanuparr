using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Converters;
using Shouldly;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Converters;

public sealed class SentinelLowercaseEnumConverterTests
{
    private readonly SentinelLowercaseEnumConverter<InstanceType> _converter = new();

    [Theory]
    [InlineData(InstanceType.Sonarr, "sonarr")]
    [InlineData(InstanceType.LazyLibrarian, "lazylibrarian")]
    public void ConvertToProvider_ReturnsLowercaseString(InstanceType input, string expected)
    {
        var result = (string?)_converter.ConvertToProvider(input);

        result.ShouldBe(expected);
    }

    [Fact]
    public void ConvertToProvider_WithTheSentinel_Throws()
    {
        // Storing it would discard the value the newer version wrote.
        Should.Throw<InvalidOperationException>(() => _converter.ConvertToProvider(InstanceType.Unknown));
    }

    [Theory]
    [InlineData("sonarr", InstanceType.Sonarr)]
    [InlineData("Sonarr", InstanceType.Sonarr)]
    [InlineData("LAZYLIBRARIAN", InstanceType.LazyLibrarian)]
    public void ConvertFromProvider_WithVariousCasings_ReturnsExpectedEnumValue(string input, InstanceType expected)
    {
        var result = (InstanceType?)_converter.ConvertFromProvider(input);

        result.ShouldBe(expected);
    }

    // A type written by a newer version loads as Unknown.
    // Ordinal 0 would read a LazyLibrarian row as Sonarr.
    [Theory]
    [InlineData("fromthefuture")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("42")]
    public void ConvertFromProvider_WithUnrecognisedString_ReturnsSentinel(string input)
    {
        var result = (InstanceType?)_converter.ConvertFromProvider(input);

        result.ShouldBe(InstanceType.Unknown);
    }
}

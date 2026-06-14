using Cleanuparr.Persistence.Converters;
using Shouldly;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Converters;

public sealed class UtcDateTimeOffsetConverterTests
{
    private readonly UtcDateTimeOffsetConverter _converter = new();

    [Fact]
    public void ConvertToProvider_WithNonUtcOffset_WritesOffsetLessUtcText()
    {
        DateTimeOffset value = new(2024, 6, 15, 12, 30, 0, TimeSpan.FromHours(2)); // 10:30 UTC

        string result = (string)_converter.ConvertToProvider(value)!;

        result.ShouldBe("2024-06-15 10:30:00.0000000");
    }

    [Fact]
    public void ConvertToProvider_WithUtcValue_WritesOffsetLessUtcText()
    {
        DateTimeOffset value = new(2024, 6, 15, 10, 30, 0, TimeSpan.Zero);

        string result = (string)_converter.ConvertToProvider(value)!;

        result.ShouldBe("2024-06-15 10:30:00.0000000");
    }

    [Theory]
    [InlineData("2025-12-19 19:56:54")]            // legacy whole-second value (no fraction, no offset)
    [InlineData("2026-06-13 20:29:50.404374")]     // legacy fractional value (no offset)
    [InlineData("2024-06-15 10:30:00.0000000")]    // value written by this converter
    public void ConvertFromProvider_ParsesLegacyAndCurrentFormatsAsUtc(string stored)
    {
        DateTimeOffset result = (DateTimeOffset)_converter.ConvertFromProvider(stored)!;

        result.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Roundtrip_PreservesInstant()
    {
        DateTimeOffset original = new(2024, 6, 15, 23, 59, 58, 123, TimeSpan.FromHours(5.5));

        string provider = (string)_converter.ConvertToProvider(original)!;
        DateTimeOffset result = (DateTimeOffset)_converter.ConvertFromProvider(provider)!;

        result.UtcDateTime.ShouldBe(original.UtcDateTime);
        result.Offset.ShouldBe(TimeSpan.Zero);
    }
}

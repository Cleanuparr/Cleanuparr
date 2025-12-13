using Cleanuparr.Persistence.Converters;
using Shouldly;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Converters;

public sealed class UtcDateTimeConverterTests
{
    private readonly UtcDateTimeConverter _converter = new();

    #region ConvertToProvider - DateTime to Database

    [Fact]
    public void ConvertToProvider_WithUtcDateTime_ReturnsSameValue()
    {
        var utcDateTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);

        var result = (DateTime?)_converter.ConvertToProvider(utcDateTime);

        result.ShouldBe(utcDateTime);
    }

    [Fact]
    public void ConvertToProvider_WithLocalDateTime_ReturnsSameValue()
    {
        var localDateTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Local);

        var result = (DateTime?)_converter.ConvertToProvider(localDateTime);

        result.ShouldBe(localDateTime);
    }

    [Fact]
    public void ConvertToProvider_WithUnspecifiedDateTime_ReturnsSameValue()
    {
        var unspecifiedDateTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Unspecified);

        var result = (DateTime?)_converter.ConvertToProvider(unspecifiedDateTime);

        result.ShouldBe(unspecifiedDateTime);
    }

    [Fact]
    public void ConvertToProvider_PreservesDateTimeKind()
    {
        var localDateTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Local);

        var result = (DateTime?)_converter.ConvertToProvider(localDateTime);

        result!.Value.Kind.ShouldBe(DateTimeKind.Local);
    }

    [Fact]
    public void ConvertToProvider_PreservesAllComponents()
    {
        var dateTime = new DateTime(2024, 6, 15, 10, 30, 45, 123, DateTimeKind.Utc);

        var result = (DateTime?)_converter.ConvertToProvider(dateTime);

        result!.Value.Year.ShouldBe(2024);
        result.Value.Month.ShouldBe(6);
        result.Value.Day.ShouldBe(15);
        result.Value.Hour.ShouldBe(10);
        result.Value.Minute.ShouldBe(30);
        result.Value.Second.ShouldBe(45);
        result.Value.Millisecond.ShouldBe(123);
    }

    #endregion

    #region ConvertFromProvider - Database to DateTime

    [Fact]
    public void ConvertFromProvider_WithUnspecifiedDateTime_ReturnsUtcKind()
    {
        var unspecifiedDateTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Unspecified);

        var result = (DateTime?)_converter.ConvertFromProvider(unspecifiedDateTime);

        result!.Value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void ConvertFromProvider_WithUtcDateTime_ReturnsUtcKind()
    {
        var utcDateTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);

        var result = (DateTime?)_converter.ConvertFromProvider(utcDateTime);

        result!.Value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void ConvertFromProvider_WithLocalDateTime_ForcesUtcKind()
    {
        // Note: DateTime.SpecifyKind does NOT convert the time, just changes the Kind
        var localDateTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Local);

        var result = (DateTime?)_converter.ConvertFromProvider(localDateTime);

        result!.Value.Kind.ShouldBe(DateTimeKind.Utc);
        // Time components remain the same (no conversion)
        result.Value.Hour.ShouldBe(10);
        result.Value.Minute.ShouldBe(30);
    }

    [Fact]
    public void ConvertFromProvider_PreservesAllComponents()
    {
        var dateTime = new DateTime(2024, 6, 15, 10, 30, 45, 123, DateTimeKind.Unspecified);

        var result = (DateTime?)_converter.ConvertFromProvider(dateTime);

        result!.Value.Year.ShouldBe(2024);
        result.Value.Month.ShouldBe(6);
        result.Value.Day.ShouldBe(15);
        result.Value.Hour.ShouldBe(10);
        result.Value.Minute.ShouldBe(30);
        result.Value.Second.ShouldBe(45);
        result.Value.Millisecond.ShouldBe(123);
    }

    [Fact]
    public void ConvertFromProvider_WithMinValue_ReturnsUtcKind()
    {
        var result = (DateTime?)_converter.ConvertFromProvider(DateTime.MinValue);

        result!.Value.Kind.ShouldBe(DateTimeKind.Utc);
        result.ShouldBe(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc));
    }

    [Fact]
    public void ConvertFromProvider_WithMaxValue_ReturnsUtcKind()
    {
        var result = (DateTime?)_converter.ConvertFromProvider(DateTime.MaxValue);

        result!.Value.Kind.ShouldBe(DateTimeKind.Utc);
        result.ShouldBe(DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc));
    }

    #endregion

    #region Roundtrip Tests

    [Fact]
    public void Roundtrip_WithUtcDateTime_PreservesValue()
    {
        var original = new DateTime(2024, 6, 15, 10, 30, 45, DateTimeKind.Utc);

        var providerValue = (DateTime?)_converter.ConvertToProvider(original);
        var result = (DateTime?)_converter.ConvertFromProvider(providerValue!.Value);

        result.ShouldBe(original);
        result!.Value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void Roundtrip_WithUnspecifiedDateTime_EndsUpAsUtc()
    {
        var original = new DateTime(2024, 6, 15, 10, 30, 45, DateTimeKind.Unspecified);

        var providerValue = (DateTime?)_converter.ConvertToProvider(original);
        var result = (DateTime?)_converter.ConvertFromProvider(providerValue!.Value);

        result!.Value.Ticks.ShouldBe(original.Ticks);
        result.Value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void ConvertFromProvider_AlwaysReturnsUtcKind(DateTimeKind inputKind)
    {
        var dateTime = new DateTime(2024, 6, 15, 10, 30, 0, inputKind);

        var result = (DateTime?)_converter.ConvertFromProvider(dateTime);

        result!.Value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void ConvertFromProvider_DoesNotConvertTime()
    {
        // This is important to understand - SpecifyKind does NOT convert time zones
        // It just changes the metadata about what time zone the DateTime represents
        var dateTime = new DateTime(2024, 6, 15, 15, 0, 0, DateTimeKind.Local);

        var result = (DateTime?)_converter.ConvertFromProvider(dateTime);

        // The hour should still be 15, not converted to UTC
        result!.Value.Hour.ShouldBe(15);
    }

    #endregion
}

using Cleanuparr.Persistence.Converters;
using Shouldly;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Converters;

public sealed class DefaultingLowercaseEnumConverterTests
{
    public enum TestEnum
    {
        First,
        Second,
        Third
    }

    private readonly DefaultingLowercaseEnumConverter<TestEnum> _converter = new(TestEnum.Second);

    [Theory]
    [InlineData(TestEnum.First, "first")]
    [InlineData(TestEnum.Third, "third")]
    public void ConvertToProvider_ReturnsLowercaseString(TestEnum input, string expected)
    {
        var result = (string?)_converter.ConvertToProvider(input);

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("first", TestEnum.First)]
    [InlineData("THIRD", TestEnum.Third)]
    public void ConvertFromProvider_WithVariousCasings_ReturnsExpectedEnumValue(string input, TestEnum expected)
    {
        var result = (TestEnum?)_converter.ConvertFromProvider(input);

        result.ShouldBe(expected);
    }

    // A setting written by a newer version reverts to what the property declares.
    // Ordinal 0 would turn log level up to Verbose.
    [Theory]
    [InlineData("fromthefuture")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("42")]
    public void ConvertFromProvider_WithUnrecognisedString_ReturnsTheFallback(string input)
    {
        var result = (TestEnum?)_converter.ConvertFromProvider(input);

        result.ShouldBe(TestEnum.Second);
    }
}

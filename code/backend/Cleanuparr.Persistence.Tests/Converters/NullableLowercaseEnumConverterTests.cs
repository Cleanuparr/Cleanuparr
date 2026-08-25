using Cleanuparr.Persistence.Converters;
using Shouldly;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Converters;

public sealed class NullableLowercaseEnumConverterTests
{
    public enum TestEnum
    {
        FirstValue,
        SecondValue,
        ALLCAPS,
        lowercase,
        MixedCase
    }

    private readonly NullableLowercaseEnumConverter<TestEnum> _converter = new();

    #region ConvertToProvider - Enum to String

    [Theory]
    [InlineData(TestEnum.FirstValue, "firstvalue")]
    [InlineData(TestEnum.SecondValue, "secondvalue")]
    [InlineData(TestEnum.ALLCAPS, "allcaps")]
    [InlineData(TestEnum.lowercase, "lowercase")]
    [InlineData(TestEnum.MixedCase, "mixedcase")]
    public void ConvertToProvider_ReturnsLowercaseString(TestEnum input, string expected)
    {
        var result = (string?)_converter.ConvertToProvider(input);

        result.ShouldBe(expected);
    }

    [Fact]
    public void ConvertToProvider_WithNull_ReturnsNull()
    {
        var result = (string?)_converter.ConvertToProvider(null);

        result.ShouldBeNull();
    }

    #endregion

    #region ConvertFromProvider - String to Enum

    [Theory]
    [InlineData("firstvalue", TestEnum.FirstValue)]
    [InlineData("FirstValue", TestEnum.FirstValue)]
    [InlineData("FIRSTVALUE", TestEnum.FirstValue)]
    [InlineData("mixedcase", TestEnum.MixedCase)]
    public void ConvertFromProvider_WithVariousCasings_ReturnsExpectedEnumValue(string input, TestEnum expected)
    {
        var result = (TestEnum?)_converter.ConvertFromProvider(input);

        result.ShouldBe(expected);
    }

    [Fact]
    public void ConvertFromProvider_WithNull_ReturnsNull()
    {
        var result = (TestEnum?)_converter.ConvertFromProvider(null);

        result.ShouldBeNull();
    }

    // A value written by a newer version reads as null rather than throwing.
    [Theory]
    [InlineData("nonexistent")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("42")]
    public void ConvertFromProvider_WithUnrecognisedString_ReturnsNull(string input)
    {
        var result = (TestEnum?)_converter.ConvertFromProvider(input);

        result.ShouldBeNull();
    }

    #endregion
}

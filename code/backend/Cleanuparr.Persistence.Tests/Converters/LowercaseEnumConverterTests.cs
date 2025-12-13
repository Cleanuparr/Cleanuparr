using Cleanuparr.Persistence.Converters;
using Shouldly;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Converters;

public sealed class LowercaseEnumConverterTests
{
    public enum TestEnum
    {
        FirstValue,
        SecondValue,
        ALLCAPS,
        lowercase,
        MixedCase
    }

    [Flags]
    public enum TestFlagsEnum
    {
        None = 0,
        Flag1 = 1,
        Flag2 = 2,
        Flag3 = 4
    }

    private readonly LowercaseEnumConverter<TestEnum> _converter = new();

    #region ConvertToProvider - Enum to String

    [Fact]
    public void ConvertToProvider_WithPascalCaseValue_ReturnsLowercaseString()
    {
        var result = (string?)_converter.ConvertToProvider(TestEnum.FirstValue);

        result.ShouldBe("firstvalue");
    }

    [Fact]
    public void ConvertToProvider_WithAllCapsValue_ReturnsLowercaseString()
    {
        var result = (string?)_converter.ConvertToProvider(TestEnum.ALLCAPS);

        result.ShouldBe("allcaps");
    }

    [Fact]
    public void ConvertToProvider_WithLowercaseValue_ReturnsLowercaseString()
    {
        var result = (string?)_converter.ConvertToProvider(TestEnum.lowercase);

        result.ShouldBe("lowercase");
    }

    [Fact]
    public void ConvertToProvider_WithMixedCaseValue_ReturnsLowercaseString()
    {
        var result = (string?)_converter.ConvertToProvider(TestEnum.MixedCase);

        result.ShouldBe("mixedcase");
    }

    [Theory]
    [InlineData(TestEnum.FirstValue, "firstvalue")]
    [InlineData(TestEnum.SecondValue, "secondvalue")]
    [InlineData(TestEnum.ALLCAPS, "allcaps")]
    [InlineData(TestEnum.lowercase, "lowercase")]
    [InlineData(TestEnum.MixedCase, "mixedcase")]
    public void ConvertToProvider_WithVariousValues_ReturnsExpectedLowercaseString(TestEnum input, string expected)
    {
        var result = (string?)_converter.ConvertToProvider(input);

        result.ShouldBe(expected);
    }

    #endregion

    #region ConvertFromProvider - String to Enum

    [Fact]
    public void ConvertFromProvider_WithLowercaseString_ReturnsEnumValue()
    {
        var result = (TestEnum?)_converter.ConvertFromProvider("firstvalue");

        result.ShouldBe(TestEnum.FirstValue);
    }

    [Fact]
    public void ConvertFromProvider_WithUppercaseString_ReturnsEnumValue()
    {
        var result = (TestEnum?)_converter.ConvertFromProvider("FIRSTVALUE");

        result.ShouldBe(TestEnum.FirstValue);
    }

    [Fact]
    public void ConvertFromProvider_WithMixedCaseString_ReturnsEnumValue()
    {
        var result = (TestEnum?)_converter.ConvertFromProvider("FirstValue");

        result.ShouldBe(TestEnum.FirstValue);
    }

    [Fact]
    public void ConvertFromProvider_WithOriginalEnumName_ReturnsEnumValue()
    {
        var result = (TestEnum?)_converter.ConvertFromProvider("ALLCAPS");

        result.ShouldBe(TestEnum.ALLCAPS);
    }

    [Theory]
    [InlineData("firstvalue", TestEnum.FirstValue)]
    [InlineData("FIRSTVALUE", TestEnum.FirstValue)]
    [InlineData("FirstValue", TestEnum.FirstValue)]
    [InlineData("secondvalue", TestEnum.SecondValue)]
    [InlineData("SECONDVALUE", TestEnum.SecondValue)]
    [InlineData("allcaps", TestEnum.ALLCAPS)]
    [InlineData("ALLCAPS", TestEnum.ALLCAPS)]
    public void ConvertFromProvider_WithVariousCasings_ReturnsExpectedEnumValue(string input, TestEnum expected)
    {
        var result = (TestEnum?)_converter.ConvertFromProvider(input);

        result.ShouldBe(expected);
    }

    [Fact]
    public void ConvertFromProvider_WithInvalidString_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => _converter.ConvertFromProvider("nonexistent"));
    }

    [Fact]
    public void ConvertFromProvider_WithEmptyString_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => _converter.ConvertFromProvider(string.Empty));
    }

    #endregion

    #region Roundtrip Tests

    [Theory]
    [InlineData(TestEnum.FirstValue)]
    [InlineData(TestEnum.SecondValue)]
    [InlineData(TestEnum.ALLCAPS)]
    [InlineData(TestEnum.lowercase)]
    [InlineData(TestEnum.MixedCase)]
    public void Roundtrip_ConvertToProviderThenBack_ReturnsOriginalValue(TestEnum original)
    {
        var providerValue = (string?)_converter.ConvertToProvider(original);
        var result = (TestEnum?)_converter.ConvertFromProvider(providerValue!);

        result.ShouldBe(original);
    }

    #endregion

    #region Flags Enum Tests

    [Fact]
    public void ConvertToProvider_WithFlagsEnum_ConvertsSingleFlag()
    {
        var flagsConverter = new LowercaseEnumConverter<TestFlagsEnum>();

        var result = (string?)flagsConverter.ConvertToProvider(TestFlagsEnum.Flag1);

        result.ShouldBe("flag1");
    }

    [Fact]
    public void ConvertToProvider_WithFlagsEnum_ConvertsNone()
    {
        var flagsConverter = new LowercaseEnumConverter<TestFlagsEnum>();

        var result = (string?)flagsConverter.ConvertToProvider(TestFlagsEnum.None);

        result.ShouldBe("none");
    }

    [Fact]
    public void ConvertFromProvider_WithFlagsEnum_ParsesSingleFlag()
    {
        var flagsConverter = new LowercaseEnumConverter<TestFlagsEnum>();

        var result = (TestFlagsEnum?)flagsConverter.ConvertFromProvider("flag2");

        result.ShouldBe(TestFlagsEnum.Flag2);
    }

    #endregion
}

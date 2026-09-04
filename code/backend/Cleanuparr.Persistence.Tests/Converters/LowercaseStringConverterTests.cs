using Cleanuparr.Persistence.Converters;
using Shouldly;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Converters;

public sealed class LowercaseStringConverterTests
{
    private readonly LowercaseStringConverter _converter = new();

    [Theory]
    [InlineData("ABCDEF01", "abcdef01")]
    [InlineData("AbCdEf01", "abcdef01")]
    [InlineData("abcdef01", "abcdef01")]
    public void ConvertToProvider_ReturnsLowercaseString(string input, string expected)
    {
        string? result = (string?)_converter.ConvertToProvider(input);

        result.ShouldBe(expected);
    }

    [Fact]
    public void ConvertToProvider_WithNull_ReturnsNull()
    {
        string? result = (string?)_converter.ConvertToProvider(null);

        result.ShouldBeNull();
    }

    // Rows written before the conversion keep their casing on read.
    [Fact]
    public void ConvertFromProvider_ReturnsTheStoredValue()
    {
        string? result = (string?)_converter.ConvertFromProvider("ABCDEF01");

        result.ShouldBe("ABCDEF01");
    }
}

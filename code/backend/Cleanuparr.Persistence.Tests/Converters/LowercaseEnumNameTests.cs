using Cleanuparr.Domain.Enums;
using Shouldly;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Converters;

public sealed class LowercaseEnumNameTests
{
    public enum TestEnum
    {
        First,
        Second,
        Third
    }

    [Theory]
    [InlineData("first", TestEnum.First)]
    [InlineData("THIRD", TestEnum.Third)]
    [InlineData(" second ", TestEnum.Second)]
    public void TryParse_WithAMemberName_Succeeds(string stored, TestEnum expected)
    {
        LowercaseEnumName.TryParse(stored, out TestEnum parsed).ShouldBeTrue();

        parsed.ShouldBe(expected);
    }

    // Enum.TryParse takes numbers, whitespace and a leading sign.
    // A stored ordinal would read as a member this build never wrote.
    [Theory]
    [InlineData("1")]
    [InlineData(" 1")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("999")]
    [InlineData("fromthefuture")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryParse_WithAnythingElse_Fails(string? stored)
    {
        LowercaseEnumName.TryParse(stored, out TestEnum parsed).ShouldBeFalse();

        parsed.ShouldBe(default);
    }
}

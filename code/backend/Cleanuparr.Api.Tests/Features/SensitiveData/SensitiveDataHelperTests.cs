using Cleanuparr.Shared.Helpers;
using Shouldly;

namespace Cleanuparr.Api.Tests.Features.SensitiveData;

public class SensitiveDataHelperTests
{
    [Fact]
    public void IsPlaceholder_WithPlaceholder_ReturnsTrue()
    {
        SensitiveDataHelper.Placeholder.IsPlaceholder().ShouldBeTrue();
    }

    [Fact]
    public void IsPlaceholder_WithAppriseStyledPlaceholder_ReturnsTrue()
    {
        $"discord://{SensitiveDataHelper.Placeholder}".IsPlaceholder().ShouldBeTrue();
    }

    [Fact]
    public void IsPlaceholder_WithNull_ReturnsFalse()
    {
        ((string?)null).IsPlaceholder().ShouldBeFalse();
    }

    [Fact]
    public void IsPlaceholder_WithEmptyString_ReturnsFalse()
    {
        "".IsPlaceholder().ShouldBeFalse();
    }

    [Fact]
    public void IsPlaceholder_WithRealValue_ReturnsFalse()
    {
        "my-secret-api-key-123".IsPlaceholder().ShouldBeFalse();
    }

    [Theory]
    [InlineData("discord://webhook_id/webhook_token", "discord://••••••••")]
    [InlineData("slack://tokenA/tokenB/tokenC", "slack://••••••••")]
    [InlineData("mailto://user:pass@gmail.com", "mailto://••••••••")]
    [InlineData("json+http://user:pass@host/path", "json+http://••••••••")]
    public void MaskAppriseUrls_SingleUrl_MasksCorrectly(string input, string expected)
    {
        SensitiveDataHelper.MaskAppriseUrls(input).ShouldBe(expected);
    }

    [Fact]
    public void MaskAppriseUrls_MultipleUrls_MasksAll()
    {
        var input = "discord://token1 slack://tokenA/tokenB";
        var result = SensitiveDataHelper.MaskAppriseUrls(input);

        result.ShouldContain("discord://••••••••");
        result.ShouldContain("slack://••••••••");
        result.ShouldNotContain("token1");
        result.ShouldNotContain("tokenA");
    }

    [Fact]
    public void MaskAppriseUrls_MultilineUrls_MasksAll()
    {
        var input = "discord://token1\nslack://tokenA/tokenB";
        var result = SensitiveDataHelper.MaskAppriseUrls(input);

        result.ShouldContain("discord://••••••••");
        result.ShouldContain("slack://••••••••");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MaskAppriseUrls_EmptyOrNull_ReturnsAsIs(string? input)
    {
        SensitiveDataHelper.MaskAppriseUrls(input!).ShouldBe(input);
    }
}

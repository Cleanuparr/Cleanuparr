using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Shouldly;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.Notification;

public sealed class WebhookConfigTests
{
    private static WebhookConfig CreateConfig(string url = "http://homeassistant.local:8123/api/webhook/abc", params string[] headers)
    {
        return new WebhookConfig { Url = url, Headers = [.. headers] };
    }

    #region IsValid Tests

    [Fact]
    public void IsValid_WithUrl_ReturnsTrue()
    {
        CreateConfig().IsValid().ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithEmptyUrl_ReturnsFalse(string url)
    {
        CreateConfig(url).IsValid().ShouldBeFalse();
    }

    #endregion

    #region Validate Tests

    [Theory]
    [InlineData("http://localhost:8123/hook")]
    [InlineData("https://n8n.example.com/webhook/abc")]
    public void Validate_WithHttpOrHttpsUrl_DoesNotThrow(string url)
    {
        Should.NotThrow(() => CreateConfig(url).Validate());
    }

    [Fact]
    public void Validate_WithEmptyUrl_ThrowsValidationException()
    {
        CustomValidationException exception = Should.Throw<CustomValidationException>(() => CreateConfig("").Validate());

        exception.Message.ShouldBe("Webhook URL is required");
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com/hook")]
    [InlineData("/relative/path")]
    public void Validate_WithNonHttpUrl_ThrowsValidationException(string url)
    {
        CustomValidationException exception = Should.Throw<CustomValidationException>(() => CreateConfig(url).Validate());

        exception.Message.ShouldBe("Webhook URL must be a valid HTTP or HTTPS URL");
    }

    [Fact]
    public void Validate_WithWellFormedHeaders_DoesNotThrow()
    {
        Should.NotThrow(() => CreateConfig(headers: ["X-Api-Key: secret", "Authorization: Bearer abc:def", "X-Empty:"]).Validate());
    }

    [Theory]
    [InlineData("X-Api-Key")]
    [InlineData(": value")]
    [InlineData("Bad Name: value")]
    [InlineData("Bad/Name: value")]
    public void Validate_WithMalformedHeader_ThrowsValidationException(string header)
    {
        Should.Throw<CustomValidationException>(() => CreateConfig(headers: header).Validate());
    }

    [Fact]
    public void Validate_WithValueContainingLineBreak_ThrowsValidationException()
    {
        Should.Throw<CustomValidationException>(() => CreateConfig(headers: "X-Api-Key: a\r\nX-Evil: b").Validate());
    }

    [Theory]
    [InlineData("X-Api-Key: a", "x-api-key: b")]
    [InlineData("Authorization: a", "Authorization: a")]
    public void Validate_WithDuplicateHeaderNames_ThrowsValidationException(string first, string second)
    {
        CustomValidationException exception = Should.Throw<CustomValidationException>(() =>
            CreateConfig(headers: [first, second]).Validate());

        exception.Message.ShouldContain("more than once");
    }

    [Theory]
    [InlineData("Content-Type: text/plain")]
    [InlineData("content-length: 10")]
    public void Validate_WithReservedHeader_ThrowsValidationException(string header)
    {
        CustomValidationException exception = Should.Throw<CustomValidationException>(() =>
            CreateConfig(headers: header).Validate());

        exception.Message.ShouldContain("set by Cleanuparr");
    }

    [Theory]
    [InlineData("Content-Language: en")]
    [InlineData("Expires: 0")]
    [InlineData("content-encoding: gzip")]
    public void Validate_WithContentHeader_ThrowsValidationException(string header)
    {
        CustomValidationException exception = Should.Throw<CustomValidationException>(() =>
            CreateConfig(headers: header).Validate());

        exception.Message.ShouldContain("cannot be sent as a request header");
    }

    #endregion

    #region TrySplitHeader Tests

    [Theory]
    [InlineData("X-Api-Key: secret", "X-Api-Key", "secret")]
    [InlineData("Authorization:Bearer abc:def", "Authorization", "Bearer abc:def")]
    [InlineData("  X-Spaced  :  value  ", "X-Spaced", "value")]
    [InlineData("X-Empty:", "X-Empty", "")]
    public void TrySplitHeader_SplitsAtTheFirstColon(string header, string expectedName, string expectedValue)
    {
        WebhookConfig.TrySplitHeader(header, out string name, out string value).ShouldBeTrue();

        name.ShouldBe(expectedName);
        value.ShouldBe(expectedValue);
    }

    [Fact]
    public void TrySplitHeader_WithoutColon_ReturnsFalse()
    {
        WebhookConfig.TrySplitHeader("no-colon", out _, out _).ShouldBeFalse();
    }

    #endregion
}

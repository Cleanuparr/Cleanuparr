using Cleanuparr.Infrastructure.Utilities;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Utilities;

public class TrackerPatternMatcherTests
{
    private readonly ILogger<TrackerPatternMatcher> _logger;
    private readonly TrackerPatternMatcher _matcher;

    public TrackerPatternMatcherTests()
    {
        _logger = Substitute.For<ILogger<TrackerPatternMatcher>>();
        _matcher = new TrackerPatternMatcher(_logger);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new TrackerPatternMatcher(null!));
    }

    [Theory]
    [InlineData("tracker.example.com", "tracker.example.com", true)]
    [InlineData("tracker.example.com", "TRACKER.EXAMPLE.COM", true)]
    [InlineData("tracker.example.com", "different.example.com", false)]
    [InlineData("", "tracker.example.com", false)]
    [InlineData("tracker.example.com", "", false)]
    [InlineData("tracker.example.com", null, false)]
    [InlineData(null, "tracker.example.com", false)]
    public void Matches_WithExactPatterns_ShouldReturnExpectedResult(string host, string pattern, bool expected)
    {
        // Act
        var result = _matcher.Matches(host, pattern);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("tracker.example.com", "*.example.com", true)]
    [InlineData("sub.tracker.example.com", "*.example.com", true)]
    [InlineData("example.com", "*.example.com", false)]
    [InlineData("tracker.different.com", "*.example.com", false)]
    [InlineData("tracker.example.com", "tracker.*", true)]
    [InlineData("tracker.anything", "tracker.*", true)]
    [InlineData("different.example.com", "tracker.*", false)]
    [InlineData("very.long.subdomain.tracker.example.com", "*.tracker.example.com", true)]
    public void Matches_WithWildcardPatterns_ShouldReturnExpectedResult(string host, string pattern, bool expected)
    {
        // Act
        var result = _matcher.Matches(host, pattern);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("tracker.example.com", "regex:tracker\\.example\\.com", true)]
    [InlineData("tracker.example.com", "regex:^tracker\\.", true)]
    [InlineData("tracker.example.com", "regex:\\.com$", true)]
    [InlineData("tracker.example.com", "regex:^sub", false)]
    [InlineData("tracker.example.org", "regex:\\.com$", false)]
    public void Matches_WithRegexPatterns_ShouldReturnExpectedResult(string host, string pattern, bool expected)
    {
        // Act
        var result = _matcher.Matches(host, pattern);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void MatchesAny_WithMultipleHosts_ShouldReturnTrueWhenAnyMatches()
    {
        // Arrange
        var hosts = new List<string> { "tracker1.example.com", "tracker2.different.com", "tracker3.another.org" }.AsReadOnly();
        var patterns = new List<string> { "*.different.com", "nonexistent.com" }.AsReadOnly();

        // Act
        var result = _matcher.MatchesAny(hosts, patterns);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void MatchesAny_WithNoMatchingHosts_ShouldReturnFalse()
    {
        // Arrange
        var hosts = new List<string> { "tracker1.example.com", "tracker2.example.com" }.AsReadOnly();
        var patterns = new List<string> { "*.different.com", "*.another.org" }.AsReadOnly();

        // Act
        var result = _matcher.MatchesAny(hosts, patterns);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void MatchesAny_WithEmptyPatterns_ShouldReturnTrue()
    {
        // Arrange
        var hosts = new List<string> { "tracker.example.com" }.AsReadOnly();
        var patterns = new List<string>().AsReadOnly();

        // Act
        var result = _matcher.MatchesAny(hosts, patterns);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void MatchesAny_WithEmptyHosts_ShouldReturnFalse()
    {
        // Arrange
        var hosts = new List<string>().AsReadOnly();
        var patterns = new List<string> { "*.example.com" }.AsReadOnly();

        // Act
        var result = _matcher.MatchesAny(hosts, patterns);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void MatchesAny_WithNullArguments_ShouldThrowArgumentNullException()
    {
        // Arrange
        var hosts = new List<string> { "tracker.example.com" }.AsReadOnly();
        var patterns = new List<string> { "*.example.com" }.AsReadOnly();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _matcher.MatchesAny(null!, patterns));
        Should.Throw<ArgumentNullException>(() => _matcher.MatchesAny(hosts, null!));
    }

    [Theory]
    [InlineData("valid.com", true, null)]
    [InlineData("*.example.com", true, null)]
    [InlineData("regex:^tracker\\.", true, null)]
    [InlineData("", false, "Pattern cannot be null or empty")]
    [InlineData(null, false, "Pattern cannot be null or empty")]
    public void ValidatePattern_WithVariousPatterns_ShouldReturnExpectedResult(string pattern, bool isValid, string expectedError)
    {
        // Act
        var result = _matcher.ValidatePattern(pattern);

        // Assert
        result.IsValid.ShouldBe(isValid);
        if (!isValid)
        {
            result.ErrorMessage.ShouldNotBeNull();
            if (expectedError != null)
            {
                result.ErrorMessage.ShouldContain(expectedError);
            }
        }
    }

    [Fact]
    public void ValidatePattern_WithInvalidRegex_ShouldReturnInvalid()
    {
        // Arrange
        var invalidRegexPattern = "regex:[invalid";

        // Act
        var result = _matcher.ValidatePattern(invalidRegexPattern);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Invalid regex pattern");
    }

    [Fact]
    public void ValidatePattern_WithDangerousRegex_ShouldReturnInvalid()
    {
        // Arrange - This is a potentially dangerous regex pattern that could cause ReDoS
        var dangerousPattern = "regex:(.*)+";

        // Act
        var result = _matcher.ValidatePattern(dangerousPattern);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("dangerous constructs");
    }

    [Fact]
    public void ValidatePattern_WithTooLongWildcardPattern_ShouldReturnInvalid()
    {
        // Arrange
        var longPattern = new string('*', 101); // 101 characters, over the limit

        // Act
        var result = _matcher.ValidatePattern(longPattern);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("too long");
    }

    [Fact]
    public void ValidatePattern_WithInvalidCharacters_ShouldReturnInvalid()
    {
        // Arrange
        var patternWithInvalidChars = "tracker<example>.com";

        // Act
        var result = _matcher.ValidatePattern(patternWithInvalidChars);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("invalid characters");
    }

    [Fact]
    public void PatternCaching_ShouldImprovePerformanceOnRepeatedCalls()
    {
        // Arrange
        var host = "tracker.example.com";
        var pattern = "*.example.com";

        // Act - First call compiles the pattern
        var result1 = _matcher.Matches(host, pattern);
        var result2 = _matcher.Matches(host, pattern);

        // Assert
        result1.ShouldBeTrue();
        result2.ShouldBeTrue();

        // Verify caching by checking cache stats
        var stats = _matcher.GetCacheStats();
        stats.CachedPatternCount.ShouldBe(1);
        stats.CacheEntries.ShouldContain(pattern);
    }

    [Fact]
    public void ClearCache_ShouldRemoveAllCachedPatterns()
    {
        // Arrange - Add some patterns to cache
        _matcher.Matches("tracker.example.com", "*.example.com");
        _matcher.Matches("tracker.test.com", "*.test.com");

        var statsBeforeClear = _matcher.GetCacheStats();
        statsBeforeClear.CachedPatternCount.ShouldBeGreaterThan(0);

        // Act
        _matcher.ClearCache();

        // Assert
        var statsAfterClear = _matcher.GetCacheStats();
        statsAfterClear.CachedPatternCount.ShouldBe(0);
    }

    [Fact]
    public void GetCacheStats_ShouldReturnCorrectStatistics()
    {
        // Arrange - Clear cache first
        _matcher.ClearCache();

        // Add specific patterns to cache
        _matcher.Matches("host1", "pattern1");
        _matcher.Matches("host2", "pattern2");
        _matcher.Matches("host1", "pattern1"); // Should use cached version

        // Act
        var stats = _matcher.GetCacheStats();

        // Assert
        stats.CachedPatternCount.ShouldBe(2);
        stats.CacheEntries.ShouldContain("pattern1");
        stats.CacheEntries.ShouldContain("pattern2");
    }

    [Theory]
    [InlineData("tracker.example.com", "*.example.com")]
    [InlineData("tracker.test.org", "regex:tracker\\.test\\.org")]
    [InlineData("sub.domain.tracker.com", "*.tracker.com")]
    public void Matches_WithValidPatternsRepeatedCalls_ShouldConsistentlyReturnTrue(string host, string pattern)
    {
        // Act - Multiple calls to ensure consistency
        var results = new bool[5];
        for (int i = 0; i < 5; i++)
        {
            results[i] = _matcher.Matches(host, pattern);
        }

        // Assert - All results should be true and consistent
        results.ShouldAllBe(x => x == true);
    }

    [Fact]
    public void Matches_WithRegexTimeout_ShouldReturnFalseAndNotThrow()
    {
        // Note: This test might be difficult to trigger consistently due to regex timeout handling
        // but verifies the timeout mechanism doesn't throw exceptions
        
        // Arrange - A complex pattern that might be slow
        var slowPattern = "regex:^(.*a.*b.*c.*d.*e.*f.*g.*h.*i.*j.*k.*l.*m.*n.*o.*p.*q.*r.*s.*t.*u.*v.*w.*x.*y.*z.*)*$";
        var host = "abcdefghijklmnopqrstuvwxyz".Repeat(10);

        // Act & Assert - Should not throw, might return false due to timeout
        Should.NotThrow(() => _matcher.Matches(host, slowPattern));
    }
}

// Extension method for test
public static class StringExtensions
{
    public static string Repeat(this string text, int count)
    {
        return string.Concat(Enumerable.Repeat(text, count));
    }
}

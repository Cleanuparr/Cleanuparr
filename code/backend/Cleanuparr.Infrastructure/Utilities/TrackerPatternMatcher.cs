using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Utilities;

/// <summary>
/// Utility class for efficient tracker host pattern matching with support for wildcards and regex patterns.
/// Provides caching and validation for optimal performance and safety.
/// </summary>
public sealed class TrackerPatternMatcher : ITrackerPatternMatcher, ITrackerPatternMatcherExtended
{
    private readonly ILogger<TrackerPatternMatcher> _logger;
    private readonly ConcurrentDictionary<string, CompiledPattern> _compiledPatterns = new();
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly RegexOptions DefaultRegexOptions = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    public TrackerPatternMatcher(ILogger<TrackerPatternMatcher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Checks if any tracker host matches any of the provided patterns.
    /// </summary>
    /// <param name="trackerHosts">List of tracker hostnames to check</param>
    /// <param name="patterns">List of patterns to match against (wildcards or regex:pattern)</param>
    /// <returns>True if any tracker matches any pattern, false otherwise</returns>
    public bool MatchesAny(IReadOnlyList<string> trackerHosts, IReadOnlyList<string> patterns)
    {
        if (trackerHosts == null) throw new ArgumentNullException(nameof(trackerHosts));
        if (patterns == null) throw new ArgumentNullException(nameof(patterns));

        // If no patterns specified, match all
        if (patterns.Count == 0)
            return true;

        // If no tracker hosts, no match possible
        if (trackerHosts.Count == 0)
            return false;

        foreach (var host in trackerHosts)
        {
            if (string.IsNullOrEmpty(host))
                continue;

            foreach (var pattern in patterns)
            {
                if (string.IsNullOrEmpty(pattern))
                    continue;

                if (Matches(host, pattern))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a tracker host matches a specific pattern.
    /// </summary>
    /// <param name="trackerHost">The tracker hostname to check</param>
    /// <param name="pattern">The pattern to match (wildcard or regex:pattern)</param>
    /// <returns>True if the host matches the pattern, false otherwise</returns>
    public bool Matches(string trackerHost, string pattern)
    {
        if (string.IsNullOrEmpty(trackerHost) || string.IsNullOrEmpty(pattern))
            return false;

        try
        {
            var compiledPattern = GetOrCompilePattern(pattern);
            return compiledPattern.Matches(trackerHost);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error matching tracker host '{Host}' against pattern '{Pattern}'", trackerHost, pattern);
            return false;
        }
    }

    /// <summary>
    /// Validates a pattern for safety and performance.
    /// </summary>
    /// <param name="pattern">The pattern to validate</param>
    /// <returns>Validation result with success status and error message if applicable</returns>
    public PatternValidationResult ValidatePattern(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return new PatternValidationResult(false, "Pattern cannot be null or empty");

        try
        {
            if (IsRegexPattern(pattern))
            {
                return ValidateRegexPattern(ExtractRegexPattern(pattern));
            }
            else
            {
                return ValidateWildcardPattern(pattern);
            }
        }
        catch (Exception ex)
        {
            return new PatternValidationResult(false, $"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the compiled pattern cache.
    /// </summary>
    public void ClearCache()
    {
        _compiledPatterns.Clear();
        _logger.LogDebug("Cleared tracker pattern cache");
    }

    /// <summary>
    /// Gets cache statistics for monitoring.
    /// </summary>
    public TrackerPatternCacheStats GetCacheStats()
    {
        return new TrackerPatternCacheStats
        {
            CachedPatternCount = _compiledPatterns.Count,
            CacheEntries = _compiledPatterns.Keys.ToList().AsReadOnly()
        };
    }

    private CompiledPattern GetOrCompilePattern(string pattern)
    {
        return _compiledPatterns.GetOrAdd(pattern, key =>
        {
            _logger.LogDebug("Compiling tracker pattern: {Pattern}", key);
            return CompilePattern(key);
        });
    }

    private CompiledPattern CompilePattern(string pattern)
    {
        if (IsRegexPattern(pattern))
        {
            var regexPattern = ExtractRegexPattern(pattern);
            var regex = new Regex(regexPattern, DefaultRegexOptions, RegexTimeout);
            return new RegexCompiledPattern(regex);
        }
        else
        {
            return new WildcardCompiledPattern(pattern);
        }
    }

    private static bool IsRegexPattern(string pattern)
    {
        return pattern.StartsWith("regex:", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractRegexPattern(string pattern)
    {
        return pattern.Substring("regex:".Length);
    }

    private PatternValidationResult ValidateRegexPattern(string regexPattern)
    {
        if (string.IsNullOrEmpty(regexPattern))
            return new PatternValidationResult(false, "Regex pattern cannot be empty");

        try
        {
            // Test regex compilation
            var regex = new Regex(regexPattern, DefaultRegexOptions, RegexTimeout);
            
            // Check for potentially dangerous patterns
            if (ContainsDangerousRegexPattern(regexPattern))
            {
                return new PatternValidationResult(false, "Pattern contains potentially dangerous constructs");
            }

            // Test performance with sample input
            var testHost = "tracker.example.com";
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            regex.IsMatch(testHost);
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > 10)
            {
                return new PatternValidationResult(false, $"Pattern is too slow ({stopwatch.ElapsedMilliseconds}ms), consider optimizing");
            }

            return new PatternValidationResult(true);
        }
        catch (ArgumentException ex)
        {
            return new PatternValidationResult(false, $"Invalid regex pattern: {ex.Message}");
        }
        catch (RegexMatchTimeoutException)
        {
            return new PatternValidationResult(false, "Regex pattern timed out during validation");
        }
    }

    private PatternValidationResult ValidateWildcardPattern(string wildcardPattern)
    {
        // Wildcards are generally safe, but we can check for basic issues
        if (wildcardPattern.Length > 100)
        {
            return new PatternValidationResult(false, "Wildcard pattern is too long (max 100 characters)");
        }

        // Check for invalid characters
        var invalidChars = new[] { '<', '>', '"', '|', '\0', '\n', '\r', '\t' };
        if (wildcardPattern.Any(invalidChars.Contains))
        {
            return new PatternValidationResult(false, "Wildcard pattern contains invalid characters");
        }

        return new PatternValidationResult(true);
    }

    private static bool ContainsDangerousRegexPattern(string pattern)
    {
        // Check for patterns that could cause ReDoS (Regular expression Denial of Service)
        var dangerousPatterns = new[]
        {
            @"\(\?\=.*\)\+",  // Nested quantifiers
            @"\(\?\!.*\)\*",  // Complex lookaheads with quantifiers
            @"\(\.\*\)\+",    // Nested .* patterns
            @"\.\*\.\*\.\*",  // Multiple .* patterns
        };

        return dangerousPatterns.Any(dangerous => 
            Regex.IsMatch(pattern, dangerous, RegexOptions.IgnoreCase));
    }

    private abstract record CompiledPattern
    {
        public abstract bool Matches(string input);
    }

    private sealed record RegexCompiledPattern(Regex Regex) : CompiledPattern
    {
        public override bool Matches(string input)
        {
            try
            {
                return Regex.IsMatch(input);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }
    }

    private sealed record WildcardCompiledPattern(string Pattern) : CompiledPattern
    {
        private readonly Regex _wildcardRegex = CompileWildcardToRegex(Pattern);

        public override bool Matches(string input)
        {
            try
            {
                return _wildcardRegex.IsMatch(input);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        private static Regex CompileWildcardToRegex(string wildcardPattern)
        {
            // Escape regex special characters except *
            var escaped = Regex.Escape(wildcardPattern).Replace("\\*", ".*");
            var regexPattern = $"^{escaped}$";
            return new Regex(regexPattern, DefaultRegexOptions, RegexTimeout);
        }
    }
}

/// <summary>
/// Interface for tracker pattern matching functionality with extended features.
/// </summary>
public interface ITrackerPatternMatcherExtended : ITrackerPatternMatcher
{
    /// <summary>
    /// Validates a pattern for safety and performance.
    /// </summary>
    PatternValidationResult ValidatePattern(string pattern);

    /// <summary>
    /// Clears the compiled pattern cache.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Gets cache statistics for monitoring.
    /// </summary>
    TrackerPatternCacheStats GetCacheStats();
}

/// <summary>
/// Result of pattern validation.
/// </summary>
public sealed record PatternValidationResult(bool IsValid, string? ErrorMessage = null);

/// <summary>
/// Cache statistics for tracker pattern matching.
/// </summary>
public sealed record TrackerPatternCacheStats
{
    public int CachedPatternCount { get; init; }
    public IReadOnlyList<string> CacheEntries { get; init; } = Array.Empty<string>();
}

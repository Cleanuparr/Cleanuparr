using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.Core;

namespace Cleanuparr.Infrastructure.Tests.TestHelpers;

/// <summary>
/// Predicates for inspecting ILogger calls recorded by NSubstitute.
/// </summary>
public static class LoggerVerificationExtensions
{
    /// <summary>
    /// Whether the logger received exactly <paramref name="count"/> log calls
    /// at the given level whose message contains the specified text.
    /// </summary>
    public static bool HasLogContaining<T>(
        this ILogger<T> logger, LogLevel level, string message, int count = 1)
    {
        return GetLogCalls(logger, level, message).Count == count;
    }

    /// <summary>
    /// Whether the logger received at least one log call
    /// at the given level whose message contains the specified text.
    /// </summary>
    public static bool HasLogContainingAtLeastOnce<T>(
        this ILogger<T> logger, LogLevel level, string message)
    {
        return GetLogCalls(logger, level, message).Count > 0;
    }

    /// <summary>
    /// Whether the logger received no log calls
    /// at the given level whose message contains the specified text.
    /// </summary>
    public static bool HasNoLogContaining<T>(
        this ILogger<T> logger, LogLevel level, string message)
    {
        return GetLogCalls(logger, level, message).Count == 0;
    }

    private static List<ICall> GetLogCalls<T>(ILogger<T> logger, LogLevel level, string message)
    {
        return logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "Log")
            .Where(c => c.GetArguments().Length > 0 && c.GetArguments()[0] is LogLevel l && l == level)
            .Where(c => c.GetArguments().Length > 2 && c.GetArguments()[2]?.ToString()?.Contains(message) == true)
            .ToList();
    }
}

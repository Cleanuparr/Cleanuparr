using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.Core;

namespace Cleanuparr.Infrastructure.Tests.TestHelpers;

/// <summary>
/// Extension methods for verifying ILogger calls with NSubstitute.
/// </summary>
public static class LoggerVerificationExtensions
{
    /// <summary>
    /// Asserts that the logger received exactly <paramref name="count"/> log calls
    /// at the given level whose message contains the specified text.
    /// </summary>
    public static void ReceivedLogContaining<T>(
        this ILogger<T> logger, LogLevel level, string message, int count = 1)
    {
        var matchingCalls = GetLogCalls(logger, level, message);
        if (matchingCalls.Count != count)
        {
            throw new Exception(
                $"Expected {count} log call(s) at {level} containing \"{message}\", " +
                $"but found {matchingCalls.Count}.");
        }
    }

    /// <summary>
    /// Asserts that the logger received at least one log call
    /// at the given level whose message contains the specified text.
    /// </summary>
    public static void ReceivedLogContainingAtLeastOnce<T>(
        this ILogger<T> logger, LogLevel level, string message)
    {
        var matchingCalls = GetLogCalls(logger, level, message);
        if (matchingCalls.Count == 0)
        {
            throw new Exception(
                $"Expected at least 1 log call at {level} containing \"{message}\", " +
                $"but found none.");
        }
    }

    /// <summary>
    /// Asserts that the logger did not receive any log calls
    /// at the given level whose message contains the specified text.
    /// </summary>
    public static void DidNotReceiveLogContaining<T>(
        this ILogger<T> logger, LogLevel level, string message)
    {
        var matchingCalls = GetLogCalls(logger, level, message);
        if (matchingCalls.Count > 0)
        {
            throw new Exception(
                $"Expected no log calls at {level} containing \"{message}\", " +
                $"but found {matchingCalls.Count}.");
        }
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

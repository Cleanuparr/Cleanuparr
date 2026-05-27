using System.Runtime.CompilerServices;

namespace Cleanuparr.Infrastructure.Interceptors;

/// <summary>
/// Wraps mutating operations so they can be short-circuited when dry-run mode is enabled.
/// Callers pass a lambda; the call-site expression is captured for logging via
/// <see cref="CallerArgumentExpressionAttribute"/> and a method name is extracted from it.
/// </summary>
public interface IDryRunInterceptor
{
    /// <summary>
    /// Executes <paramref name="action"/> unless dry-run mode is enabled, in which case the
    /// operation is skipped and the call is logged.
    /// </summary>
    /// <param name="action">The synchronous operation to execute.</param>
    /// <param name="expression">Auto-populated call-site expression used to log the skipped method name.</param>
    void Intercept(
        Action action,
        [CallerArgumentExpression(nameof(action))] string? expression = null);

    /// <summary>
    /// Awaits <paramref name="action"/> unless dry-run mode is enabled, in which case the
    /// operation is skipped and the call is logged.
    /// </summary>
    /// <param name="action">The asynchronous operation to execute.</param>
    /// <param name="expression">Auto-populated call-site expression used to log the skipped method name.</param>
    Task InterceptAsync(
        Func<Task> action,
        [CallerArgumentExpression(nameof(action))] string? expression = null);

    /// <summary>
    /// Awaits <paramref name="action"/> and returns its result unless dry-run mode is enabled,
    /// in which case the operation is skipped and <c>default(T)</c> is returned.
    /// </summary>
    /// <typeparam name="T">The result type returned by <paramref name="action"/>.</typeparam>
    /// <param name="action">The asynchronous operation to execute.</param>
    /// <param name="expression">Auto-populated call-site expression used to log the skipped method name.</param>
    /// <returns>The result of <paramref name="action"/>, or <c>default</c> when dry-run mode is enabled.</returns>
    Task<T?> InterceptAsync<T>(
        Func<Task<T>> action,
        [CallerArgumentExpression(nameof(action))] string? expression = null);

    /// <summary>
    /// Returns whether dry-run mode is currently enabled in the persisted general configuration.
    /// </summary>
    Task<bool> IsDryRunEnabled();
}

using Microsoft.Extensions.Time.Testing;

namespace Cleanuparr.Infrastructure.Tests.TestHelpers;

/// <summary>
/// Clock control for handlers that wait on a <see cref="FakeTimeProvider"/>.
/// </summary>
public static class FakeTimeProviderExtensions
{
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private const int MaxAdvances = 5;

    /// <summary>
    /// Awaits a running handler, advancing the clock past the delays it waits on.
    /// </summary>
    /// <remarks>
    /// A handler registers its timer asynchronously, so a single advance can land before the timer
    /// exists and leave it waiting for a due time the clock has already passed. Advancing a few
    /// times covers that race, and the cap keeps the clock close enough for file age assertions.
    /// </remarks>
    /// <exception cref="TimeoutException">The handler did not finish after the last advance.</exception>
    public static async Task AdvanceUntilCompleted(this FakeTimeProvider timeProvider, Task execution)
    {
        for (int advance = 0; advance < MaxAdvances && !execution.IsCompleted; advance++)
        {
            timeProvider.Advance(Step);
            await Task.Delay(20);
        }

        await execution.WaitAsync(Timeout);
    }
}

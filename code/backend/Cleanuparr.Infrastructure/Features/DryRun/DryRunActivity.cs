namespace Cleanuparr.Infrastructure.Features.DryRun;

/// <summary>
/// Counts dry runs in flight.
/// </summary>
public sealed class DryRunActivity
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _active;

    /// <summary>
    /// Whether any dry run is in flight.
    /// </summary>
    public bool IsActive => Volatile.Read(ref _active) > 0;

    /// <summary>
    /// Marks the start of a dry run.
    /// Waits for any in-progress purge to finish first.
    /// </summary>
    public async Task EnterAsync()
    {
        await _gate.WaitAsync();

        try
        {
            Interlocked.Increment(ref _active);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Marks the end of a dry run.
    /// </summary>
    public void Exit()
    {
        Interlocked.Decrement(ref _active);
    }

    /// <summary>
    /// Runs the given action only if no dry run is in flight.
    /// Blocks new dry runs while the action runs.
    /// </summary>
    public async Task<bool> RunIfIdleAsync(Func<Task> action)
    {
        await _gate.WaitAsync();

        try
        {
            if (Volatile.Read(ref _active) > 0)
            {
                return false;
            }

            await action();
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }
}

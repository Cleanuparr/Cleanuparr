namespace Cleanuparr.Infrastructure.Features.DryRun;

/// <summary>
/// Counts dry runs in flight.
/// </summary>
public sealed class DryRunActivity
{
    private int _active;

    /// <summary>
    /// Whether any dry run is in flight.
    /// </summary>
    public bool IsActive => Volatile.Read(ref _active) > 0;

    /// <summary>
    /// Marks the start of a dry run.
    /// </summary>
    public void Enter()
    {
        Interlocked.Increment(ref _active);
    }

    /// <summary>
    /// Marks the end of a dry run.
    /// </summary>
    public void Exit()
    {
        Interlocked.Decrement(ref _active);
    }
}

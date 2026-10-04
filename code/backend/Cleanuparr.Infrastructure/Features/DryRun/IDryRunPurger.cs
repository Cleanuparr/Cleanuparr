namespace Cleanuparr.Infrastructure.Features.DryRun;

/// <summary>
/// Deletes the data a dry run left behind.
/// </summary>
public interface IDryRunPurger
{
    /// <summary>
    /// Purges dry-run data.
    /// Skips while dry runs are active.
    /// </summary>
    Task PurgeAsync();

    /// <summary>
    /// Purges only when the stored setting has dry run off.
    /// Ignores the run's sticky value.
    /// </summary>
    Task PurgeIfDryRunOffAsync();
}

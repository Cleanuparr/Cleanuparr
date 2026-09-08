namespace Cleanuparr.Infrastructure.Features.Arr.ForceImport;

public enum ForceImportOutcome
{
    /// <summary>
    /// Force import does not apply, so the queue cleaner runs its failed import checks.
    /// </summary>
    NotApplicable,

    /// <summary>
    /// Force import can still work on a later run, so the download waits without a strike.
    /// </summary>
    Deferred,
}

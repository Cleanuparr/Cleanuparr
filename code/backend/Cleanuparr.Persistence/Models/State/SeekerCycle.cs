using System.ComponentModel.DataAnnotations;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Persistence.Models.State;

/// <summary>
/// One Seeker cycle of an arr instance: every eligible unit searched once.
/// </summary>
public sealed record SeekerCycle
{
    /// <summary>
    /// The cycle id, shared with the search events and units searched in it
    /// </summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The arr instance this cycle belongs to
    /// </summary>
    public Guid ArrInstanceId { get; set; }

    /// <summary>
    /// When the cycle opened
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// When the last eligible unit got searched. Null while the cycle runs.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Why the cycle ended. Null while the cycle runs.
    /// </summary>
    public SeekerCycleCompletionReason? CompletionReason { get; set; }

    /// <summary>
    /// Eligible units when the cycle opened
    /// </summary>
    public int EligibleAtStart { get; set; }

    /// <summary>
    /// Eligible units on the latest run
    /// </summary>
    public int EligibleNow { get; set; }

    /// <summary>
    /// Eligible units on the latest run that this cycle has not searched and that are not in the queue
    /// </summary>
    public int RemainingUnits { get; set; }

    /// <summary>
    /// Searches triggered in this cycle
    /// </summary>
    public int SearchedUnits { get; set; }

    /// <summary>
    /// Searches in this cycle that grabbed a release
    /// </summary>
    public int GrabbedUnits { get; set; }

    /// <summary>
    /// Whether the cycle ran in dry run mode
    /// </summary>
    public bool IsDryRun { get; set; }
}

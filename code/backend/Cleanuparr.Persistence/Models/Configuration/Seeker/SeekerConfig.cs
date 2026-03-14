using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Enums;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.Seeker;

/// <summary>
/// Global configuration for the Seeker job that searches for missing items and quality upgrades
/// </summary>
public sealed record SeekerConfig : IJobConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

    public bool Enabled { get; set; }

    public string CronExpression { get; set; } = "0 0/5 * * * ?";

    /// <summary>
    /// Indicates whether to use the CronExpression directly or convert from a user-friendly schedule
    /// </summary>
    public bool UseAdvancedScheduling { get; set; } = false;

    /// <summary>
    /// Strategy used to select which items to search
    /// </summary>
    public SelectionStrategy SelectionStrategy { get; set; } = SelectionStrategy.BalancedWeighted;

    /// <summary>
    /// Only search monitored items
    /// </summary>
    public bool MonitoredOnly { get; set; } = true;

    /// <summary>
    /// Skip items that already meet their quality cutoff
    /// </summary>
    public bool UseCutoff { get; set; }

    /// <summary>
    /// Process one instance per run to spread indexer load
    /// </summary>
    public bool UseRoundRobin { get; set; } = true;

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }
    }
}

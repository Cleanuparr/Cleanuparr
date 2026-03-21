using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Shared.Helpers;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.Seeker;

/// <summary>
/// The Seeker job is always running; only its behavior is configurable.
/// </summary>
public sealed record SeekerConfig : IConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Master toggle for all searching (reactive and proactive).
    /// When disabled, no searches are triggered at all.
    /// </summary>
    public bool SearchEnabled { get; set; } = true;

    /// <summary>
    /// Interval in minutes between Seeker runs. Controls how frequently searches are triggered.
    /// Valid values: 2, 3, 4, 5, 6, 10, 12, 15, 20, 30 (must divide 60 evenly for cron compatibility).
    /// </summary>
    public ushort SearchInterval { get; set; } = Constants.DefaultSearchIntervalMinutes;

    /// <summary>
    /// Enables proactive searching for missing items and quality upgrades.
    /// When disabled, only reactive searches (replacement after removal) are performed.
    /// </summary>
    public bool ProactiveSearchEnabled { get; set; }

    /// <summary>
    /// Strategy used to select which items to search during proactive searches
    /// </summary>
    public SelectionStrategy SelectionStrategy { get; set; } = SelectionStrategy.BalancedWeighted;

    /// <summary>
    /// Only search monitored items during proactive searches
    /// </summary>
    public bool MonitoredOnly { get; set; } = true;

    /// <summary>
    /// Skip items that already meet their quality cutoff during proactive searches
    /// </summary>
    public bool UseCutoff { get; set; }

    /// <summary>
    /// Search items whose custom format score is below the quality profile's cutoff format score
    /// </summary>
    public bool UseCustomFormatScore { get; set; }

    /// <summary>
    /// Process one instance per run to spread indexer load during proactive searches
    /// </summary>
    public bool UseRoundRobin { get; set; } = true;

    public void Validate()
    {
        if (SearchInterval < Constants.MinSearchIntervalMinutes)
        {
            throw new ValidationException(
                $"{nameof(SearchInterval)} must be at least {Constants.MinSearchIntervalMinutes} minute(s)");
        }

        if (SearchInterval > Constants.MaxSearchIntervalMinutes)
        {
            throw new ValidationException(
                $"{nameof(SearchInterval)} must be at most {Constants.MaxSearchIntervalMinutes} minutes");
        }

        if (!new List<int> { 2, 3, 4, 5, 6, 10, 12, 15, 20, 30 }.Contains(SearchInterval))
        {
            throw new ValidationException($"Invalid search interval {SearchInterval}");
        }
    }

    /// <summary>
    /// Generates the internal cron expression from the SearchInterval.
    /// </summary>
    public string ToCronExpression() => $"0 */{SearchInterval} * * * ?";
}

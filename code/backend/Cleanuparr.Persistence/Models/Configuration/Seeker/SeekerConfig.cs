using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Shared.Helpers;

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
    /// Process one instance per run to spread indexer load during proactive searches
    /// </summary>
    public bool UseRoundRobin { get; set; } = true;

    /// <summary>
    /// Hours to wait after content is released before searching.
    /// Gives indexers time to process new releases. 0 = disabled.
    /// </summary>
    public int PostReleaseGraceHours { get; set; } = 6;

    /// <summary>
    /// Pauses proactive search during the quiet hours window.
    /// Replacement searches keep running.
    /// </summary>
    public bool QuietHoursEnabled { get; set; }

    /// <summary>
    /// Local time of day when quiet hours begin (inclusive).
    /// </summary>
    public TimeOnly QuietHoursStart { get; set; } = new(23, 0);

    /// <summary>
    /// Local time of day when quiet hours end (exclusive).
    /// An end earlier than the start crosses midnight.
    /// </summary>
    public TimeOnly QuietHoursEnd { get; set; } = new(7, 0);

    public void Validate()
    {
        if (SearchInterval < Constants.MinSearchIntervalMinutes)
        {
            throw new CustomValidationException(
                $"{nameof(SearchInterval)} must be at least {Constants.MinSearchIntervalMinutes} minute(s)");
        }

        if (SearchInterval > Constants.MaxSearchIntervalMinutes)
        {
            throw new CustomValidationException(
                $"{nameof(SearchInterval)} must be at most {Constants.MaxSearchIntervalMinutes} minutes");
        }

        if (!new List<int> { 2, 3, 4, 5, 6, 10, 12, 15, 20, 30, 60, 120, 180, 240, 360 }.Contains(SearchInterval))
        {
            throw new CustomValidationException($"Invalid search interval {SearchInterval}");
        }

        if (SearchEnabled && ProactiveSearchEnabled && PostReleaseGraceHours is < 0 or > 72)
        {
            throw new CustomValidationException($"{nameof(PostReleaseGraceHours)} must be between 0 and 72");
        }

        if (SearchEnabled && ProactiveSearchEnabled && QuietHoursEnabled && QuietHoursStart == QuietHoursEnd)
        {
            throw new CustomValidationException($"{nameof(QuietHoursStart)} and {nameof(QuietHoursEnd)} must differ");
        }
    }

    /// <summary>
    /// Whether proactive search must wait at this local time of day.
    /// </summary>
    public bool IsQuietTime(TimeOnly time)
    {
        if (!QuietHoursEnabled)
        {
            return false;
        }

        if (QuietHoursStart < QuietHoursEnd)
        {
            return time >= QuietHoursStart && time < QuietHoursEnd;
        }

        return time >= QuietHoursStart || time < QuietHoursEnd;
    }

    /// <summary>
    /// Generates the internal cron expression from the SearchInterval.
    /// </summary>
    public string ToCronExpression()
    {
        if (SearchInterval < 60)
        {
            return $"0 */{SearchInterval} * * * ?";
        }

        if (SearchInterval == 60)
        {
            return "0 0 * * * ?";
        }

        return $"0 0 */{SearchInterval / 60} * * ?";
    }
}

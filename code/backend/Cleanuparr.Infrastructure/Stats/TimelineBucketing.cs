using System.Globalization;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Stats;

public static class TimelineBucketing
{
    public static TimelineBucketSize DefaultFor(int hours) =>
        hours <= 24 ? TimelineBucketSize.Hour : TimelineBucketSize.Day;

    /// <summary>
    /// SQLite expression that maps the <c>timestamp</c> column to a bucket key. Hour keys are
    /// "yyyy-MM-dd HH"; day/week/month keys are dates ("yyyy-MM-dd"), where week is the Monday of the
    /// week and month is the first of the month. Fractional seconds are trimmed via substr so SQLite's
    /// date functions parse the stored "yyyy-MM-dd HH:mm:ss.fffffff" format cleanly.
    /// </summary>
    public static string BucketExpr(TimelineBucketSize size) => size switch
    {
        TimelineBucketSize.Hour => "substr(timestamp, 1, 13)",
        TimelineBucketSize.Day => "substr(timestamp, 1, 10)",
        TimelineBucketSize.Week => "date(substr(timestamp, 1, 19), '-' || ((strftime('%w', substr(timestamp, 1, 19)) + 6) % 7) || ' days')",
        TimelineBucketSize.Month => "strftime('%Y-%m-01', substr(timestamp, 1, 19))",
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, null),
    };

    public static DateTimeOffset ParseKey(string key, TimelineBucketSize size)
    {
        string format = size == TimelineBucketSize.Hour ? "yyyy-MM-dd HH" : "yyyy-MM-dd";
        DateTime parsed = DateTime.ParseExact(
            key,
            format,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        return new DateTimeOffset(parsed, TimeSpan.Zero);
    }

    public static IEnumerable<DateTimeOffset> Buckets(DateTimeOffset cutoff, DateTimeOffset now, TimelineBucketSize size)
    {
        switch (size)
        {
            case TimelineBucketSize.Hour:
                for (DateTimeOffset hour = TruncateToHour(cutoff); hour <= TruncateToHour(now); hour = hour.AddHours(1))
                {
                    yield return hour;
                }
                break;

            case TimelineBucketSize.Day:
                for (DateTimeOffset day = TruncateToDay(cutoff); day <= TruncateToDay(now); day = day.AddDays(1))
                {
                    yield return day;
                }
                break;

            case TimelineBucketSize.Week:
                for (DateTimeOffset week = TruncateToWeek(cutoff); week <= TruncateToWeek(now); week = week.AddDays(7))
                {
                    yield return week;
                }
                break;

            case TimelineBucketSize.Month:
                for (DateTimeOffset month = TruncateToMonth(cutoff); month <= TruncateToMonth(now); month = month.AddMonths(1))
                {
                    yield return month;
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(size), size, null);
        }
    }

    private static DateTimeOffset TruncateToHour(DateTimeOffset value)
    {
        DateTime u = value.UtcDateTime;
        return new DateTimeOffset(new DateTime(u.Year, u.Month, u.Day, u.Hour, 0, 0, DateTimeKind.Utc), TimeSpan.Zero);
    }

    private static DateTimeOffset TruncateToDay(DateTimeOffset value) =>
        new(value.UtcDateTime.Date, TimeSpan.Zero);

    private static DateTimeOffset TruncateToWeek(DateTimeOffset value)
    {
        DateTime date = value.UtcDateTime.Date;
        int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return new DateTimeOffset(date.AddDays(-daysSinceMonday), TimeSpan.Zero);
    }

    private static DateTimeOffset TruncateToMonth(DateTimeOffset value)
    {
        DateTime u = value.UtcDateTime;
        return new DateTimeOffset(new DateTime(u.Year, u.Month, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero);
    }
}

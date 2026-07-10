using System.Globalization;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Stats;

public static class TimelineBucketing
{
    public static TimelineBucketSize DefaultFor(int hours) =>
        hours <= 24 ? TimelineBucketSize.Hour : TimelineBucketSize.Day;

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

using System.Globalization;

namespace Cleanuparr.Infrastructure.Stats;

public static class TimelineBucketing
{
    public static bool IsHourly(int hours) => hours <= 24;

    public static int KeyLength(bool hourly) => hourly ? 13 : 10;

    public static DateTimeOffset ParseKey(string key, bool hourly)
    {
        string format = hourly ? "yyyy-MM-dd HH" : "yyyy-MM-dd";
        DateTime parsed = DateTime.ParseExact(
            key,
            format,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        return new DateTimeOffset(parsed, TimeSpan.Zero);
    }

    public static IEnumerable<DateTimeOffset> Buckets(DateTimeOffset cutoff, DateTimeOffset now, bool hourly)
    {
        if (hourly)
        {
            DateTimeOffset start = TruncateToHour(cutoff);
            DateTimeOffset end = TruncateToHour(now);
            for (DateTimeOffset hour = start; hour <= end; hour = hour.AddHours(1))
            {
                yield return hour;
            }
        }
        else
        {
            DateTime start = cutoff.UtcDateTime.Date;
            DateTime end = now.UtcDateTime.Date;
            for (DateTime day = start; day <= end; day = day.AddDays(1))
            {
                yield return new DateTimeOffset(day, TimeSpan.Zero);
            }
        }
    }

    private static DateTimeOffset TruncateToHour(DateTimeOffset value)
    {
        DateTime u = value.UtcDateTime;
        return new DateTimeOffset(new DateTime(u.Year, u.Month, u.Day, u.Hour, 0, 0, DateTimeKind.Utc), TimeSpan.Zero);
    }
}

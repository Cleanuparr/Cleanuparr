namespace Cleanuparr.Api.Common;

public static class TimelineWindow
{
    public const int MinHours = 1;

    public const int MaxHours = 8760;

    public static int ClampHours(int hours) => Math.Clamp(hours, MinHours, MaxHours);
}

namespace Cleanuparr.Shared.Helpers;

public static class StaticConfiguration
{
    public static TimeSpan TriggerValue { get; set; } = TimeSpan.Zero;

    // Quartz cron for running at minute 0 of every hour
    public const string BlacklistSyncCron = "0 0 * * * ?";
}
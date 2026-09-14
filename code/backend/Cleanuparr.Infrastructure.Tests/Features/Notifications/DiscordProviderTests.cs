using System.Globalization;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Discord;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class DiscordProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IDiscordProxy _proxy = Substitute.For<IDiscordProxy>();
    private readonly DiscordProvider _provider;

    public DiscordProviderTests()
    {
        DiscordConfig config = new()
        {
            Id = Guid.NewGuid(),
            WebhookUrl = "https://discord.example.com/webhook",
        };

        _provider = new DiscordProvider("TestDiscord", NotificationProviderType.Discord, config, _proxy, new FakeTimeProvider(Now));
    }

    [Fact]
    public async Task SendNotificationAsync_StampsTheEmbedWithTheCurrentTime()
    {
        NotificationContext context = new()
        {
            EventType = NotificationEventType.DownloadStopped,
            Title = "Download stopped",
            Description = "A torrent was removed",
        };

        await _provider.SendNotificationAsync(context);

        DiscordPayload payload = (DiscordPayload)_proxy.ReceivedCalls().Single().GetArguments()[0]!;
        string timestamp = payload.Embeds.Single().Timestamp!;

        DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            .ShouldBe(Now);
    }
}

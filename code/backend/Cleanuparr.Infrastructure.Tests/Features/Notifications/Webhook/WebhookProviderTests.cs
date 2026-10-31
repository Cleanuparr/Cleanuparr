using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Features.Notifications.Webhook;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications.Webhook;

public class WebhookProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly IWebhookProxy _proxy = Substitute.For<IWebhookProxy>();
    private readonly WebhookConfig _config = new() { Url = "https://hooks.example.com/abc" };
    private readonly WebhookProvider _provider;

    public WebhookProviderTests()
    {
        _provider = new WebhookProvider("TestWebhook", NotificationProviderType.Webhook, _config, _proxy, new FakeTimeProvider(Now));
    }

    private async Task<WebhookPayload> SendAsync(NotificationContext context)
    {
        await _provider.SendNotificationAsync(context);

        return (WebhookPayload)_proxy.ReceivedCalls().Single().GetArguments()[0]!;
    }

    [Fact]
    public async Task SendNotificationAsync_MapsContextToPayload()
    {
        NotificationContext context = new()
        {
            EventType = NotificationEventType.StalledStrike,
            Title = "Strike received",
            Description = "A torrent stalled",
            Severity = EventSeverity.Warning,
        };

        WebhookPayload payload = await SendAsync(context);

        payload.EventType.ShouldBe(NotificationEventType.StalledStrike);
        payload.Title.ShouldBe("Strike received");
        payload.Description.ShouldBe("A torrent stalled");
        payload.Severity.ShouldBe(EventSeverity.Warning);
    }

    [Fact]
    public async Task SendNotificationAsync_PassesTheConfigToTheProxy()
    {
        await SendAsync(new NotificationContext { Title = "t", Description = "d" });

        WebhookConfig config = (WebhookConfig)_proxy.ReceivedCalls().Single().GetArguments()[1]!;
        config.ShouldBeSameAs(_config);
    }

    [Fact]
    public async Task SendNotificationAsync_StampsThePayloadWithTheCurrentTime()
    {
        WebhookPayload payload = await SendAsync(new NotificationContext { Title = "t", Description = "d" });

        payload.Timestamp.ShouldBe(Now);
    }

    [Fact]
    public async Task SendNotificationAsync_WithoutImage_LeavesImageNull()
    {
        WebhookPayload payload = await SendAsync(new NotificationContext { Title = "t", Description = "d" });

        payload.Image.ShouldBeNull();
    }

    [Fact]
    public async Task SendNotificationAsync_WithImage_SendsItAsPlainUrl()
    {
        NotificationContext context = new()
        {
            Title = "t",
            Description = "d",
            Image = new Uri("https://example.com/poster.jpg")
        };

        WebhookPayload payload = await SendAsync(context);

        payload.Image.ShouldBe("https://example.com/poster.jpg");
    }

    [Theory]
    [InlineData("Removed from client?", "removedFromClient")]
    [InlineData("Url", "url")]
    [InlineData("Strike count", "strikeCount")]
    [InlineData("Instance type", "instanceType")]
    [InlineData("hash", "hash")]
    public async Task SendNotificationAsync_ConvertsDataLabelsToCamelCase(string label, string expectedKey)
    {
        NotificationContext context = new()
        {
            Title = "t",
            Description = "d",
            Data = new Dictionary<string, string> { [label] = "value" }
        };

        WebhookPayload payload = await SendAsync(context);

        payload.Data.ShouldContainKeyAndValue(expectedKey, "value");
        payload.Data.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SendNotificationAsync_KeepsDataValuesAsBuilt()
    {
        NotificationContext context = new()
        {
            Title = "t",
            Description = "d",
            Data = new Dictionary<string, string>
            {
                ["Removed from client?"] = "True",
                ["Strike count"] = "5"
            }
        };

        WebhookPayload payload = await SendAsync(context);

        payload.Data["removedFromClient"].ShouldBe("True");
        payload.Data["strikeCount"].ShouldBe("5");
    }
}

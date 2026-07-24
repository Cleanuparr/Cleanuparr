using System.Net;
using System.Text.Json.Nodes;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Infrastructure.Features.Notifications.Discord;
using Cleanuparr.Infrastructure.Features.Notifications.Gotify;
using Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;
using Cleanuparr.Infrastructure.Features.Notifications.Ntfy;
using Cleanuparr.Infrastructure.Features.Notifications.Telegram;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

/// <summary>
/// Characterization tests locking the exact outbound JSON body each notification proxy produces.
/// Written against the current Newtonsoft output and must stay green after the System.Text.Json migration.
/// Bodies are compared field-by-field on parsed JSON (order-independent); special characters are asserted
/// on the raw string to pin escaping behavior.
/// </summary>
public class NotificationPayloadSerializationTests
{
    private const string SpecialChars = "Ubuntu & <b>bold</b> it's";

    private static (T proxy, FakeHttpMessageHandler handler) CreateProxy<T>(Func<IHttpClientFactory, T> factory)
    {
        FakeHttpMessageHandler handler = new();
        handler.SetupResponse(HttpStatusCode.OK);
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Constants.HttpClientWithRetryName).Returns(new HttpClient(handler));
        return (factory(httpClientFactory), handler);
    }

    private static JsonObject ParseBody(FakeHttpMessageHandler handler)
    {
        string? body = handler.CapturedRequestBodies[0];
        body.ShouldNotBeNull();
        return JsonNode.Parse(body)!.AsObject();
    }

    private static IEnumerable<string> Keys(JsonObject obj)
    {
        return obj.Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Discord_OmitsNulls_UsesExplicitNames_PreservesSpecialChars()
    {
        (DiscordProxy proxy, FakeHttpMessageHandler handler) = CreateProxy(f =>
            new DiscordProxy(Substitute.For<ILogger<DiscordProxy>>(), f));

        DiscordPayload payload = new()
        {
            Username = null,
            AvatarUrl = "https://example.com/a.png?x=1&y=2",
            Embeds = [new DiscordEmbed { Title = SpecialChars, Description = "d", Color = 123 }]
        };

        await proxy.SendNotification(payload, new DiscordConfig { WebhookUrl = "https://discord.com/api/webhooks/1/a" });

        JsonObject body = ParseBody(handler);
        Keys(body).ShouldBe(["avatar_url", "embeds"]);
        body["avatar_url"]!.GetValue<string>().ShouldBe("https://example.com/a.png?x=1&y=2");

        JsonObject embed = body["embeds"]!.AsArray()[0]!.AsObject();
        Keys(embed).ShouldBe(["color", "description", "fields", "title"]);
        embed["title"]!.GetValue<string>().ShouldBe(SpecialChars);
        embed["color"]!.GetValue<int>().ShouldBe(123);
        embed["fields"]!.AsArray().Count.ShouldBe(0);

        handler.CapturedRequestBodies[0]!.ShouldContain(SpecialChars);
        handler.CapturedRequestBodies[0]!.ShouldContain("x=1&y=2");
    }

    [Fact]
    public async Task Ntfy_OmitsNulls_PreservesSpecialChars()
    {
        (NtfyProxy proxy, FakeHttpMessageHandler handler) = CreateProxy(f => new NtfyProxy(f));

        NtfyPayload payload = new()
        {
            Topic = "my-topic",
            Message = SpecialChars,
            Title = null,
            Priority = null,
            Tags = ["a", "b"],
            Click = null
        };

        await proxy.SendNotification(payload, new NtfyConfig { ServerUrl = "https://ntfy.example.com" });

        JsonObject body = ParseBody(handler);
        Keys(body).ShouldBe(["message", "tags", "topic"]);
        body["topic"]!.GetValue<string>().ShouldBe("my-topic");
        body["message"]!.GetValue<string>().ShouldBe(SpecialChars);
        body["tags"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldBe(["a", "b"]);

        handler.CapturedRequestBodies[0]!.ShouldContain(SpecialChars);
    }

    [Fact]
    public async Task Apprise_OmitsNulls_UsesDefaults_PreservesSpecialChars()
    {
        (AppriseProxy proxy, FakeHttpMessageHandler handler) = CreateProxy(f => new AppriseProxy(f));

        ApprisePayload payload = new() { Title = "T", Body = SpecialChars };

        await proxy.SendNotification(payload, new AppriseConfig { Url = "https://apprise.example.com", Key = "mykey" });

        JsonObject body = ParseBody(handler);
        Keys(body).ShouldBe(["body", "format", "title", "type"]);
        body["title"]!.GetValue<string>().ShouldBe("T");
        body["body"]!.GetValue<string>().ShouldBe(SpecialChars);
        body["type"]!.GetValue<string>().ShouldBe("info");
        body["format"]!.GetValue<string>().ShouldBe("text");

        handler.CapturedRequestBodies[0]!.ShouldContain(SpecialChars);
    }

    [Fact]
    public async Task Telegram_SendMessage_OmitsNulls_PreservesSpecialChars()
    {
        (TelegramProxy proxy, FakeHttpMessageHandler handler) = CreateProxy(f => new TelegramProxy(f));

        TelegramPayload payload = new()
        {
            ChatId = "123",
            Text = SpecialChars,
            MessageThreadId = null,
            DisableNotification = false
        };

        await proxy.SendNotification(payload, "TOKEN");

        JsonObject body = ParseBody(handler);
        Keys(body).ShouldBe(["chat_id", "disable_notification", "disable_web_page_preview", "parse_mode", "text"]);
        body["chat_id"]!.GetValue<string>().ShouldBe("123");
        body["disable_notification"]!.GetValue<bool>().ShouldBeFalse();
        body["disable_web_page_preview"]!.GetValue<bool>().ShouldBeTrue();
        body["parse_mode"]!.GetValue<string>().ShouldBe("HTML");
        body["text"]!.GetValue<string>().ShouldBe(SpecialChars);

        handler.CapturedRequestBodies[0]!.ShouldContain(SpecialChars);
    }

    [Fact]
    public async Task Gotify_OmitsNulls_PreservesSpecialChars()
    {
        (GotifyProxy proxy, FakeHttpMessageHandler handler) = CreateProxy(f =>
            new GotifyProxy(Substitute.For<ILogger<GotifyProxy>>(), f));

        GotifyPayload payload = new() { Title = SpecialChars, Message = "m", Priority = 5, Extras = null };

        await proxy.SendNotification(payload, new GotifyConfig { ServerUrl = "https://gotify.example.com", ApplicationToken = "tok" });

        JsonObject body = ParseBody(handler);
        Keys(body).ShouldBe(["message", "priority", "title"]);
        body["title"]!.GetValue<string>().ShouldBe(SpecialChars);
        body["message"]!.GetValue<string>().ShouldBe("m");
        body["priority"]!.GetValue<int>().ShouldBe(5);

        handler.CapturedRequestBodies[0]!.ShouldContain(SpecialChars);
    }

    [Fact]
    public async Task Notifiarr_IncludesNulls()
    {
        (NotifiarrProxy proxy, FakeHttpMessageHandler handler) = CreateProxy(f =>
            new NotifiarrProxy(Substitute.For<ILogger<NotifiarrProxy>>(), f));

        NotifiarrPayload payload = new()
        {
            Notification = new NotifiarrNotification { Update = false, Event = null },
            Discord = null!
        };

        await proxy.SendNotification(payload, new NotifiarrConfig { ApiKey = "0123456789" });

        JsonObject body = ParseBody(handler);
        body.ContainsKey("discord").ShouldBeTrue();
        body["discord"].ShouldBeNull();

        JsonObject notification = body["notification"]!.AsObject();
        notification["name"]!.GetValue<string>().ShouldBe("Cleanuparr");
        notification.ContainsKey("event").ShouldBeTrue();
        notification["event"].ShouldBeNull();
        notification["update"]!.GetValue<bool>().ShouldBeFalse();
    }
}

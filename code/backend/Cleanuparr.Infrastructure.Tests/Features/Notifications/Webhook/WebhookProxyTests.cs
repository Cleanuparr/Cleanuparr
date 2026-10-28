using System.Net;
using System.Text.Json.Nodes;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Webhook;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications.Webhook;

public class WebhookProxyTests
{
    private readonly ILogger<WebhookProxy> _logger = Substitute.For<ILogger<WebhookProxy>>();
    private readonly FakeHttpMessageHandler _httpMessageHandler = new();
    private readonly WebhookProxy _proxy;

    public WebhookProxyTests()
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory
            .CreateClient(Constants.HttpClientWithRetryName)
            .Returns(new HttpClient(_httpMessageHandler));

        _proxy = new WebhookProxy(_logger, httpClientFactory);
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);
    }

    private static WebhookPayload CreatePayload()
    {
        return new WebhookPayload
        {
            EventType = NotificationEventType.QueueItemDeleted,
            Title = "Test Title",
            Description = "Test Description",
            Severity = EventSeverity.Important,
            Data = new Dictionary<string, string> { ["removedFromClient"] = "True" },
            Image = null,
            Timestamp = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero)
        };
    }

    private static WebhookConfig CreateConfig(WebhookMethod method = WebhookMethod.Post, List<string>? headers = null)
    {
        return new WebhookConfig
        {
            Url = "http://homeassistant.local:8123/api/webhook/abc123",
            Method = method,
            Headers = headers ?? []
        };
    }

    [Theory]
    [InlineData(WebhookMethod.Post, "POST")]
    [InlineData(WebhookMethod.Put, "PUT")]
    public async Task SendNotification_UsesConfiguredMethod(WebhookMethod method, string expected)
    {
        await _proxy.SendNotification(CreatePayload(), CreateConfig(method));

        _httpMessageHandler.CapturedRequests[0].Method.Method.ShouldBe(expected);
    }

    [Fact]
    public async Task SendNotification_SendsToConfiguredUrl()
    {
        await _proxy.SendNotification(CreatePayload(), CreateConfig());

        _httpMessageHandler.CapturedRequests[0].RequestUri?.ToString()
            .ShouldBe("http://homeassistant.local:8123/api/webhook/abc123");
    }

    [Fact]
    public async Task SendNotification_SendsHeadersWithValues()
    {
        List<string> headers = ["X-Api-Key: secret", "Authorization: Bearer abc:def"];

        await _proxy.SendNotification(CreatePayload(), CreateConfig(headers: headers));

        HttpRequestMessage request = _httpMessageHandler.CapturedRequests[0];
        request.Headers.GetValues("X-Api-Key").ShouldBe(["secret"]);
        request.Headers.GetValues("Authorization").ShouldBe(["Bearer abc:def"]);
    }

    [Fact]
    public async Task SendNotification_SendsJsonBodyWithEveryKey()
    {
        await _proxy.SendNotification(CreatePayload(), CreateConfig());

        HttpRequestMessage request = _httpMessageHandler.CapturedRequests[0];
        request.Content?.Headers.ContentType?.MediaType.ShouldBe("application/json");

        JsonObject body = JsonNode.Parse(_httpMessageHandler.CapturedRequestBodies[0]!)!.AsObject();
        body.Select(pair => pair.Key).OrderBy(key => key, StringComparer.Ordinal)
            .ShouldBe(["data", "description", "eventType", "image", "severity", "timestamp", "title"]);
        body["eventType"]!.GetValue<string>().ShouldBe("queueItemDeleted");
        body["severity"]!.GetValue<string>().ShouldBe("important");
        body["image"].ShouldBeNull();
        body["timestamp"]!.GetValue<string>().ShouldBe("2026-10-28T12:00:00+00:00");
        body["data"]!["removedFromClient"]!.GetValue<string>().ShouldBe("True");
    }

    [Fact]
    public async Task SendNotification_LogsTraceWithContent()
    {
        await _proxy.SendNotification(CreatePayload(), CreateConfig());

        _logger.HasLogContaining(LogLevel.Trace, "sending notification").ShouldBeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "rejected the credentials")]
    [InlineData(HttpStatusCode.Forbidden, "rejected the credentials")]
    [InlineData(HttpStatusCode.NotFound, "not found")]
    [InlineData(HttpStatusCode.BadGateway, "service unavailable")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "service unavailable")]
    [InlineData(HttpStatusCode.GatewayTimeout, "service unavailable")]
    [InlineData(HttpStatusCode.InternalServerError, "Unable to send notification")]
    public async Task SendNotification_MapsStatusCodeToWebhookException(HttpStatusCode statusCode, string expectedMessage)
    {
        _httpMessageHandler.SetupThrow(new HttpRequestException("Error", null, statusCode));

        WebhookException exception = await Should.ThrowAsync<WebhookException>(() =>
            _proxy.SendNotification(CreatePayload(), CreateConfig()));

        exception.Message.ShouldContain(expectedMessage);
    }

    [Fact]
    public async Task SendNotification_WhenNetworkError_ThrowsWebhookException()
    {
        _httpMessageHandler.SetupThrow(new HttpRequestException("Network error"));

        WebhookException exception = await Should.ThrowAsync<WebhookException>(() =>
            _proxy.SendNotification(CreatePayload(), CreateConfig()));

        exception.Message.ShouldContain("Unable to send notification");
    }
}

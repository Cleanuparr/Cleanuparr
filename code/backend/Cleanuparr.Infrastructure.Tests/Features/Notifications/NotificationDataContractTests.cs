using System.Threading.Channels;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Features.Notifications.Webhook;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

/// <summary>
/// Pins the Data keys of every notification.
/// The webhook docs list these keys in camelCase, so update them on any label rename.
/// </summary>
public class NotificationDataContractTests
{
    private readonly Channel<NotificationMessage> _queue = Channel.CreateUnbounded<NotificationMessage>();
    private readonly NotificationPublisher _publisher;

    public NotificationDataContractTests()
    {
        IDryRunInterceptor dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        dryRunInterceptor.InterceptAsync(Arg.Any<Func<Task>>(), Arg.Any<string?>())
            .ReturnsForAnyArgs(ci => ci.ArgAt<Func<Task>>(0).Invoke());

        _publisher = new NotificationPublisher(
            Substitute.For<ILogger<NotificationPublisher>>(),
            dryRunInterceptor,
            _queue.Writer);
    }

    private static void SetupArrContext()
    {
        ContextProvider.Set(nameof(QueueRecord), new QueueRecord
        {
            Id = 1,
            Title = "Test Show",
            DownloadId = "ABCD1234",
            Status = "Downloading",
            Protocol = "torrent"
        });
        ContextProvider.Set(nameof(InstanceType), InstanceType.Sonarr);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceUrl, new Uri("http://sonarr.local"));
        ContextProvider.Set(ContextProvider.Keys.Version, 1f);
    }

    private static void SetupDownloadClientContext()
    {
        ContextProvider.Set(ContextProvider.Keys.ItemName, "Test Download");
        ContextProvider.Set(ContextProvider.Keys.DownloadClientUrl, new Uri("http://downloadclient.local"));
        ContextProvider.Set(ContextProvider.Keys.Hash, "HASH123");
    }

    private NotificationContext ReadPublished()
    {
        _queue.Reader.Count.ShouldBe(1);
        _queue.Reader.TryRead(out NotificationMessage? message).ShouldBeTrue();
        return message!.Context;
    }

    private static async Task<WebhookPayload> ToWebhookPayload(NotificationContext context)
    {
        IWebhookProxy proxy = Substitute.For<IWebhookProxy>();
        WebhookProvider provider = new(
            "Webhook",
            NotificationProviderType.Webhook,
            new WebhookConfig { Url = "https://hooks.example.com/abc" },
            proxy,
            TimeProvider.System);

        await provider.SendNotificationAsync(context);

        return (WebhookPayload)proxy.ReceivedCalls().Single().GetArguments()[0]!;
    }

    private static async Task AssertKeys(NotificationContext context, string[] labels, string[] webhookKeys)
    {
        context.Data.Keys.OrderBy(key => key, StringComparer.Ordinal).ShouldBe(labels.OrderBy(key => key, StringComparer.Ordinal));

        WebhookPayload payload = await ToWebhookPayload(context);
        payload.Data.Count.ShouldBe(context.Data.Count);
        payload.Data.Keys.OrderBy(key => key, StringComparer.Ordinal).ShouldBe(webhookKeys.OrderBy(key => key, StringComparer.Ordinal));
    }

    private static readonly string[] StrikeLabels = ["Strike type", "Strike count", "Hash", "Instance type", "Url"];
    private static readonly string[] StrikeKeys = ["strikeType", "strikeCount", "hash", "instanceType", "url"];

    [Theory]
    [InlineData(StrikeType.Stalled)]
    [InlineData(StrikeType.SlowSpeed)]
    [InlineData(StrikeType.SlowTime)]
    public async Task Strike_WithMatchedRule_HasRuleName(StrikeType strikeType)
    {
        SetupArrContext();
        ContextProvider.Set<QueueRule>(new StallRule { Name = "Test Rule" });

        await _publisher.NotifyStrike(strikeType, 1);

        await AssertKeys(ReadPublished(), [.. StrikeLabels, "Rule name"], [.. StrikeKeys, "ruleName"]);
    }

    [Theory]
    [InlineData(StrikeType.FailedImport)]
    [InlineData(StrikeType.Errored)]
    [InlineData(StrikeType.DownloadingMetadata)]
    public async Task Strike_WithoutRule_OmitsRuleName(StrikeType strikeType)
    {
        SetupArrContext();

        await _publisher.NotifyStrike(strikeType, 1);

        await AssertKeys(ReadPublished(), StrikeLabels, StrikeKeys);
    }

    [Fact]
    public async Task QueueItemDeleted_HasPinnedKeys()
    {
        SetupArrContext();

        await _publisher.NotifyQueueItemDeleted(true, DeleteReason.Stalled);

        await AssertKeys(
            ReadPublished(),
            ["Reason", "Removed from client?", "Hash", "Instance type", "Url"],
            ["reason", "removedFromClient", "hash", "instanceType", "url"]);
    }

    [Fact]
    public async Task ForceImported_HasPinnedKeys()
    {
        SetupArrContext();

        await _publisher.NotifyForceImported();

        await AssertKeys(ReadPublished(), ["Hash", "Instance type", "Url"], ["hash", "instanceType", "url"]);
    }

    [Fact]
    public async Task DownloadCleaned_HasPinnedKeys()
    {
        SetupDownloadClientContext();

        await _publisher.NotifyDownloadCleaned(2.5, TimeSpan.FromHours(48), "movies", CleanReason.MaxRatioReached);

        await AssertKeys(
            ReadPublished(),
            ["Url", "Hash", "Category", "Ratio", "Seeding hours"],
            ["url", "hash", "category", "ratio", "seedingHours"]);
    }

    [Fact]
    public async Task DownloadStopped_HasPinnedKeys()
    {
        SetupDownloadClientContext();

        await _publisher.NotifyDownloadStopped(2.5, TimeSpan.FromHours(48), "movies", CleanReason.MaxRatioReached);

        await AssertKeys(
            ReadPublished(),
            ["Url", "Hash", "Category", "Ratio", "Seeding hours"],
            ["url", "hash", "category", "ratio", "seedingHours"]);
    }

    [Fact]
    public async Task CategoryChanged_HasPinnedKeys()
    {
        SetupDownloadClientContext();

        await _publisher.NotifyCategoryChanged("old", "new");

        await AssertKeys(
            ReadPublished(),
            ["Url", "hash", "Old category", "New category"],
            ["url", "hash", "oldCategory", "newCategory"]);
    }

    [Fact]
    public async Task CategoryChanged_ForTag_HasTagKey()
    {
        SetupDownloadClientContext();

        await _publisher.NotifyCategoryChanged("old", "new", isTag: true);

        await AssertKeys(ReadPublished(), ["Url", "hash", "Tag"], ["url", "hash", "tag"]);
    }

    [Fact]
    public async Task SearchTriggered_HasPinnedKeys()
    {
        SetupArrContext();

        await _publisher.NotifySearchTriggered("Test Show", SeekerSearchType.Proactive, SeekerSearchReason.Missing);

        await AssertKeys(
            ReadPublished(),
            ["Instance type", "Url", "Item", "Search type", "Search reason"],
            ["instanceType", "url", "item", "searchType", "searchReason"]);
    }

    [Fact]
    public async Task SearchItemGrabbed_HasPinnedKeys()
    {
        await _publisher.NotifySearchItemGrabbed("Test Show", ["Release.1"], InstanceType.Sonarr, "http://sonarr.local");

        await AssertKeys(
            ReadPublished(),
            ["Item", "Grabbed", "Instance type", "Url"],
            ["item", "grabbed", "instanceType", "url"]);
    }

    [Fact]
    public async Task Test_HasPinnedKeys()
    {
        INotificationProvider provider = Substitute.For<INotificationProvider>();
        INotificationProviderFactory factory = Substitute.For<INotificationProviderFactory>();
        factory.CreateProvider(Arg.Any<NotificationProviderDto>()).Returns(provider);

        NotificationService service = new(
            Substitute.For<ILogger<NotificationService>>(),
            Substitute.For<INotificationConfigurationService>(),
            factory,
            TimeProvider.System);

        await service.SendTestNotificationAsync(new NotificationProviderDto
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Type = NotificationProviderType.Webhook,
            IsEnabled = true,
            Configuration = new object()
        });

        NotificationContext context = (NotificationContext)provider.ReceivedCalls().Single().GetArguments()[0]!;

        await AssertKeys(context, ["Test time", "Provider type"], ["testTime", "providerType"]);
    }
}

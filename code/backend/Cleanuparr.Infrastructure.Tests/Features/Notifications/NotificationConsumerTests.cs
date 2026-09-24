using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Consumers;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class NotificationConsumerTests
{
    private readonly INotificationConfigurationService _configurationService;
    private readonly INotificationProviderFactory _providerFactory;
    private readonly NotificationConsumer _consumer;

    public NotificationConsumerTests()
    {
        _configurationService = Substitute.For<INotificationConfigurationService>();
        _providerFactory = Substitute.For<INotificationProviderFactory>();

        NotificationService notificationService = new(
            Substitute.For<ILogger<NotificationService>>(),
            _configurationService,
            _providerFactory,
            TimeProvider.System);
        _consumer = new NotificationConsumer(notificationService);
    }

    private static NotificationContext CreateContext(NotificationEventType eventType)
    {
        return new NotificationContext
        {
            EventType = eventType,
            Title = "Test title",
            Description = "Test description",
        };
    }

    private static NotificationProviderDto CreateProviderDto(string name = "TestProvider")
    {
        return new NotificationProviderDto
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = NotificationProviderType.Notifiarr,
            IsEnabled = true,
            Events = new NotificationEventFlags(),
            Configuration = new { ApiKey = "test" }
        };
    }

    private static ConsumeContext<NotificationMessage> CreateConsumeContext(NotificationMessage message)
    {
        ConsumeContext<NotificationMessage> context = Substitute.For<ConsumeContext<NotificationMessage>>();
        context.Message.Returns(message);
        return context;
    }

    [Fact]
    public async Task Consume_WithConfiguredProviders_SendsToAll()
    {
        NotificationProviderDto providerDto1 = CreateProviderDto("Provider1");
        NotificationProviderDto providerDto2 = CreateProviderDto("Provider2");
        INotificationProvider provider1 = Substitute.For<INotificationProvider>();
        INotificationProvider provider2 = Substitute.For<INotificationProvider>();

        NotificationMessage message = new(NotificationEventType.FailedImportStrike, CreateContext(NotificationEventType.FailedImportStrike));

        _configurationService.GetProvidersForEventAsync(NotificationEventType.FailedImportStrike)
            .Returns(new List<NotificationProviderDto> { providerDto1, providerDto2 });
        _providerFactory.CreateProvider(providerDto1).Returns(provider1);
        _providerFactory.CreateProvider(providerDto2).Returns(provider2);

        await _consumer.Consume(CreateConsumeContext(message));

        await provider1.Received(1).SendNotificationAsync(message.Context);
        await provider2.Received(1).SendNotificationAsync(message.Context);
    }

    [Fact]
    public async Task Consume_WhenOneProviderThrows_OthersStillReceiveNotification()
    {
        NotificationProviderDto providerDto1 = CreateProviderDto("Provider1");
        NotificationProviderDto providerDto2 = CreateProviderDto("Provider2");
        INotificationProvider provider1 = Substitute.For<INotificationProvider>();
        INotificationProvider provider2 = Substitute.For<INotificationProvider>();

        provider1.SendNotificationAsync(Arg.Any<NotificationContext>())
            .ThrowsAsync(new Exception("Provider1 failed"));

        NotificationMessage message = new(NotificationEventType.StalledStrike, CreateContext(NotificationEventType.StalledStrike));

        _configurationService.GetProvidersForEventAsync(NotificationEventType.StalledStrike)
            .Returns(new List<NotificationProviderDto> { providerDto1, providerDto2 });
        _providerFactory.CreateProvider(providerDto1).Returns(provider1);
        _providerFactory.CreateProvider(providerDto2).Returns(provider2);

        await _consumer.Consume(CreateConsumeContext(message));

        await provider2.Received(1).SendNotificationAsync(message.Context);
    }

    [Fact]
    public async Task Consume_WhenNoProvidersConfigured_DoesNotCreateAnyProvider()
    {
        NotificationMessage message = new(NotificationEventType.QueueItemDeleted, CreateContext(NotificationEventType.QueueItemDeleted));

        _configurationService.GetProvidersForEventAsync(NotificationEventType.QueueItemDeleted)
            .Returns(new List<NotificationProviderDto>());

        await _consumer.Consume(CreateConsumeContext(message));

        _providerFactory.DidNotReceive().CreateProvider(Arg.Any<NotificationProviderDto>());
    }

    [Fact]
    public async Task Consume_WhenProviderLookupThrows_DoesNotThrow()
    {
        NotificationMessage message = new(NotificationEventType.QueueItemDeleted, CreateContext(NotificationEventType.QueueItemDeleted));

        _configurationService.GetProvidersForEventAsync(Arg.Any<NotificationEventType>())
            .ThrowsAsync(new InvalidOperationException("db locked"));

        ConsumeContext<NotificationMessage> ctx = CreateConsumeContext(message);

        await Should.NotThrowAsync(() => _consumer.Consume(ctx));

        _providerFactory.DidNotReceive().CreateProvider(Arg.Any<NotificationProviderDto>());
    }
}

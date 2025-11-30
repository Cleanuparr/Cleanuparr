using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Hubs;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient.TestHelpers;

/// <summary>
/// Factory for creating EventPublisher instances for testing
/// </summary>
public static class TestEventPublisherFactory
{
    public static EventPublisher Create()
    {
        // Create a mock EventsContext with minimal setup
        var eventsContextMock = new Mock<EventsContext>(
            new DbContextOptionsBuilder<EventsContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        return new EventPublisher(
            eventsContextMock.Object,
            new Mock<IHubContext<AppHub>>().Object,
            new Mock<ILogger<EventPublisher>>().Object,
            new Mock<INotificationPublisher>().Object,
            new Mock<IDryRunInterceptor>().Object);
    }
}

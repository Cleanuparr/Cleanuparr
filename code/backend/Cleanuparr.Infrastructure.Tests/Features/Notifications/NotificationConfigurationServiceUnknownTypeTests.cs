using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

/// <summary>
/// A provider type only a newer version knows has no implementation here.
/// It must never reach the cache the senders read.
/// Backed by SQLite: EF refuses to write an unknown value.
/// </summary>
public sealed class NotificationConfigurationServiceUnknownTypeTests : IAsyncLifetime
{
    private readonly SqliteTestDatabase _database = SqliteTestDatabase.Create("notification-unknown-type");

    public async Task InitializeAsync()
    {
        await using DataContext context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync()
    {
        _database.Dispose();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetActiveProvidersAsync_SkipsAProviderOfAnUnknownType()
    {
        Guid unknownId = await AddProviderAsync("from the future");
        Guid knownId = await AddProviderAsync("supported");

        await using DataContext seed = CreateContext();
        await seed.Database.ExecuteSqlRawAsync(
            "UPDATE notification_configs SET type = 'fromthefuture' WHERE id = {0}",
            unknownId);

        await using DataContext context = CreateContext();
        NotificationConfigurationService service = new(
            context,
            Substitute.For<ILogger<NotificationConfigurationService>>());

        List<NotificationProviderDto> providers = await service.GetActiveProvidersAsync();

        providers.Select(p => p.Id).ShouldBe([knownId]);
    }

    private async Task<Guid> AddProviderAsync(string name)
    {
        await using DataContext context = CreateContext();

        NotificationConfig config = new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = NotificationProviderType.Notifiarr,
            IsEnabled = true,
            OnStalledStrike = true,
            NotifiarrConfiguration = new NotifiarrConfig
            {
                Id = Guid.NewGuid(),
                ApiKey = "testapikey1234567890",
                ChannelId = "123456789012345678",
            },
        };

        context.Set<NotificationConfig>().Add(config);
        await context.SaveChangesAsync();

        return config.Id;
    }

    private DataContext CreateContext() => _database.CreateContext<DataContext>();
}

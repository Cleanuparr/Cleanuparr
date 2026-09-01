using Cleanuparr.Domain.Entities.HealthCheck;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Health;

public sealed class HealthCheckServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DataContext> _options;
    private readonly IDownloadServiceFactory _downloadServiceFactory = Substitute.For<IDownloadServiceFactory>();

    public HealthCheckServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(_connection)
            .Options;

        using DataContext context = new(_options);
        context.Database.EnsureCreated();

        IDownloadService downloadService = Substitute.For<IDownloadService>();
        downloadService.HealthCheckAsync().Returns(new HealthCheckResult { IsHealthy = true });
        _downloadServiceFactory.GetDownloadService(Arg.Any<DownloadClientConfig>()).Returns(downloadService);
    }

    [Fact]
    public async Task Checking_all_clients_caches_every_enabled_client()
    {
        Seed(SeedClient("first"), SeedClient("second"));

        HealthCheckService service = BuildService();
        await service.CheckAllClientsHealthAsync();

        service.GetAllClientHealth().Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_deleted_client_stops_being_reported()
    {
        DownloadClientConfig removed = SeedClient("removed");
        Seed(removed, SeedClient("kept"));

        HealthCheckService service = BuildService();
        await service.CheckAllClientsHealthAsync();

        await using (DataContext context = new(_options))
        {
            context.DownloadClients.Remove(await context.DownloadClients.FirstAsync(c => c.Id == removed.Id));
            await context.SaveChangesAsync();
        }

        await service.CheckAllClientsHealthAsync();

        service.GetAllClientHealth().Keys.ShouldBe([Seeded("kept")]);
        service.GetClientHealth(removed.Id).ShouldBeNull();
    }

    [Fact]
    public async Task A_disabled_client_stops_being_reported()
    {
        DownloadClientConfig disabled = SeedClient("disabled");
        Seed(disabled, SeedClient("kept"));

        HealthCheckService service = BuildService();
        await service.CheckAllClientsHealthAsync();

        await using (DataContext context = new(_options))
        {
            DownloadClientConfig stored = await context.DownloadClients.FirstAsync(c => c.Id == disabled.Id);
            stored.Enabled = false;
            await context.SaveChangesAsync();
        }

        await service.CheckAllClientsHealthAsync();

        service.GetAllClientHealth().Keys.ShouldBe([Seeded("kept")]);
    }

    [Fact]
    public async Task A_failed_sweep_keeps_what_was_already_cached()
    {
        Seed(SeedClient("first"));

        HealthCheckService service = BuildService();
        await service.CheckAllClientsHealthAsync();

        // A sweep that cannot read the database returns empty; that is not proof nothing exists.
        _connection.Close();
        await service.CheckAllClientsHealthAsync();

        service.GetAllClientHealth().Count.ShouldBe(1);
    }

    private readonly Dictionary<string, Guid> _seededIds = new();

    private Guid Seeded(string name) => _seededIds[name];

    private DownloadClientConfig SeedClient(string name)
    {
        DownloadClientConfig client = new()
        {
            Id = Guid.NewGuid(),
            Enabled = true,
            Name = name,
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
        };

        _seededIds[name] = client.Id;

        return client;
    }

    private void Seed(params DownloadClientConfig[] clients)
    {
        using DataContext context = new(_options);
        context.DownloadClients.AddRange(clients);
        context.SaveChanges();
    }

    private HealthCheckService BuildService()
    {
        ServiceCollection services = new();
        services.AddScoped(_ => new DataContext(_options));
        services.AddScoped(_ => _downloadServiceFactory);

        return new HealthCheckService(
            NullLogger<HealthCheckService>.Instance,
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}

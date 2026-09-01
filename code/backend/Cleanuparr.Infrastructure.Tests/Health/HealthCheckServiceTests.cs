using Cleanuparr.Domain.Entities.HealthCheck;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
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
    private readonly IInstanceHealthChecker _instanceHealthChecker = Substitute.For<IInstanceHealthChecker>();
    private readonly Dictionary<string, Guid> _seededIds = new();

    public HealthCheckServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(_connection)
            .Options;

        using DataContext context = new(_options);
        context.Database.EnsureCreated();

        ProbeReturns(new HealthCheckResult { IsHealthy = true });
    }

    #region Single client check

    [Fact]
    public async Task A_healthy_client_reports_the_probe_result()
    {
        Seed(SeedClient("qbit"));
        ProbeReturns(new HealthCheckResult { IsHealthy = true, ResponseTime = TimeSpan.FromMilliseconds(42) });

        HealthStatus status = await BuildService().CheckClientHealthAsync(Seeded("qbit"));

        status.IsHealthy.ShouldBeTrue();
        status.ClientName.ShouldBe("qbit");
        status.ClientTypeName.ShouldBe(DownloadClientTypeName.qBittorrent);
        status.ResponseTime.ShouldBe(TimeSpan.FromMilliseconds(42));
        status.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task An_unhealthy_probe_keeps_its_error_message()
    {
        Seed(SeedClient("qbit"));
        ProbeReturns(new HealthCheckResult { IsHealthy = false, ErrorMessage = "401 Unauthorized" });

        HealthStatus status = await BuildService().CheckClientHealthAsync(Seeded("qbit"));

        status.IsHealthy.ShouldBeFalse();
        status.ErrorMessage.ShouldBe("401 Unauthorized");
        status.ClientName.ShouldBe("qbit");
    }

    [Fact]
    public async Task An_unknown_client_id_reports_unhealthy_and_is_cached()
    {
        Guid missing = Guid.NewGuid();

        HealthCheckService service = BuildService();
        HealthStatus status = await service.CheckClientHealthAsync(missing);

        status.IsHealthy.ShouldBeFalse();
        status.ErrorMessage.ShouldBe("Client not found in configuration");
        service.GetClientHealth(missing).ShouldBe(status);
    }

    [Fact]
    public async Task A_probe_that_throws_reports_unhealthy_with_the_reason()
    {
        Seed(SeedClient("qbit"));
        ProbeThrows(new InvalidOperationException("connection refused"));

        HealthStatus status = await BuildService().CheckClientHealthAsync(Seeded("qbit"));

        status.IsHealthy.ShouldBeFalse();
        status.ErrorMessage.ShouldBe("Error: connection refused");
    }

    #endregion

    #region Client health events

    [Fact]
    public async Task A_first_unhealthy_check_counts_as_a_degradation()
    {
        Seed(SeedClient("qbit"));
        ProbeReturns(new HealthCheckResult { IsHealthy = false, ErrorMessage = "down" });

        HealthCheckService service = BuildService();
        List<ClientHealthChangedEventArgs> raised = [];
        service.ClientHealthChanged += (_, e) => raised.Add(e);

        await service.CheckClientHealthAsync(Seeded("qbit"));

        ClientHealthChangedEventArgs single = raised.ShouldHaveSingleItem();
        single.IsDegraded.ShouldBeTrue();
        single.IsRecovered.ShouldBeFalse();
    }

    [Fact]
    public async Task Going_from_healthy_to_unhealthy_raises_a_degradation()
    {
        Seed(SeedClient("qbit"));

        HealthCheckService service = BuildService();
        await service.CheckClientHealthAsync(Seeded("qbit"));

        List<ClientHealthChangedEventArgs> raised = [];
        service.ClientHealthChanged += (_, e) => raised.Add(e);
        ProbeReturns(new HealthCheckResult { IsHealthy = false, ErrorMessage = "down" });

        await service.CheckClientHealthAsync(Seeded("qbit"));

        raised.ShouldHaveSingleItem().IsDegraded.ShouldBeTrue();
    }

    [Fact]
    public async Task Going_from_unhealthy_to_healthy_raises_a_recovery()
    {
        Seed(SeedClient("qbit"));
        ProbeReturns(new HealthCheckResult { IsHealthy = false, ErrorMessage = "down" });

        HealthCheckService service = BuildService();
        await service.CheckClientHealthAsync(Seeded("qbit"));

        List<ClientHealthChangedEventArgs> raised = [];
        service.ClientHealthChanged += (_, e) => raised.Add(e);
        ProbeReturns(new HealthCheckResult { IsHealthy = true });

        await service.CheckClientHealthAsync(Seeded("qbit"));

        ClientHealthChangedEventArgs single = raised.ShouldHaveSingleItem();
        single.IsRecovered.ShouldBeTrue();
        single.IsDegraded.ShouldBeFalse();
    }

    [Fact]
    public async Task An_unchanged_state_raises_nothing()
    {
        Seed(SeedClient("qbit"));

        HealthCheckService service = BuildService();
        await service.CheckClientHealthAsync(Seeded("qbit"));

        List<ClientHealthChangedEventArgs> raised = [];
        service.ClientHealthChanged += (_, e) => raised.Add(e);

        await service.CheckClientHealthAsync(Seeded("qbit"));

        raised.ShouldBeEmpty();
    }

    #endregion

    #region Reading the cache

    [Fact]
    public void An_unknown_client_has_no_cached_status()
    {
        BuildService().GetClientHealth(Guid.NewGuid()).ShouldBeNull();
    }

    [Fact]
    public async Task The_returned_map_is_a_copy()
    {
        Seed(SeedClient("qbit"));

        HealthCheckService service = BuildService();
        await service.CheckAllClientsHealthAsync();

        service.GetAllClientHealth().Clear();

        service.GetAllClientHealth().Count.ShouldBe(1);
    }

    #endregion

    #region Client sweep

    [Fact]
    public async Task Checking_all_clients_caches_every_enabled_client()
    {
        Seed(SeedClient("first"), SeedClient("second"));

        HealthCheckService service = BuildService();
        await service.CheckAllClientsHealthAsync();

        service.GetAllClientHealth().Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_disabled_client_is_never_probed()
    {
        DownloadClientConfig disabled = SeedClient("disabled");
        disabled.Enabled = false;
        Seed(disabled, SeedClient("enabled"));

        HealthCheckService service = BuildService();
        IDictionary<Guid, HealthStatus> results = await service.CheckAllClientsHealthAsync();

        results.Keys.ShouldBe([Seeded("enabled")]);
    }

    [Fact]
    public async Task One_failing_client_does_not_stop_the_sweep()
    {
        Seed(SeedClient("good"), SeedClient("bad"));
        ProbeThrows(new InvalidOperationException("connection refused"), forClientNamed: "bad");

        HealthCheckService service = BuildService();
        IDictionary<Guid, HealthStatus> results = await service.CheckAllClientsHealthAsync();

        results.Count.ShouldBe(2);
        results[Seeded("good")].IsHealthy.ShouldBeTrue();
        results[Seeded("bad")].IsHealthy.ShouldBeFalse();
    }

    [Fact]
    public async Task A_deleted_client_stops_being_reported()
    {
        DownloadClientConfig removed = SeedClient("removed");
        Seed(removed, SeedClient("kept"));

        HealthCheckService service = BuildService();
        await service.CheckAllClientsHealthAsync();

        await DeleteClientAsync(removed.Id);
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
    public async Task Dropping_a_client_announces_the_removal()
    {
        DownloadClientConfig removed = SeedClient("removed");
        Seed(removed, SeedClient("kept"));

        HealthCheckService service = BuildService();
        await service.CheckAllClientsHealthAsync();

        List<Guid> announced = [];
        service.ClientHealthRemoved += (_, e) => announced.Add(e.ClientId);

        await DeleteClientAsync(removed.Id);
        await service.CheckAllClientsHealthAsync();

        announced.ShouldBe([removed.Id]);
    }

    [Fact]
    public async Task A_sweep_that_drops_nothing_announces_nothing()
    {
        Seed(SeedClient("kept"));

        HealthCheckService service = BuildService();
        await service.CheckAllClientsHealthAsync();

        List<Guid> announced = [];
        service.ClientHealthRemoved += (_, e) => announced.Add(e.ClientId);

        await service.CheckAllClientsHealthAsync();

        announced.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_sweep_keeps_what_was_already_cached()
    {
        Seed(SeedClient("first"));

        HealthCheckService service = BuildService();
        await service.CheckAllClientsHealthAsync();

        // A sweep that cannot read the database returns empty; that is not proof nothing exists.
        _connection.Close();
        IDictionary<Guid, HealthStatus> results = await service.CheckAllClientsHealthAsync();

        results.ShouldBeEmpty();
        service.GetAllClientHealth().Count.ShouldBe(1);
    }

    #endregion

    #region Single arr instance check

    [Fact]
    public async Task A_reachable_arr_instance_reports_healthy()
    {
        SeedArr(InstanceType.Sonarr, "main", enabled: true);

        ArrHealthStatus status = await BuildService().CheckArrInstanceHealthAsync(Seeded("main"));

        status.IsHealthy.ShouldBeTrue();
        status.InstanceName.ShouldBe("main");
        status.InstanceType.ShouldBe(InstanceType.Sonarr);
        status.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task An_unknown_arr_instance_id_reports_unhealthy_and_is_cached()
    {
        Guid missing = Guid.NewGuid();

        HealthCheckService service = BuildService();
        ArrHealthStatus status = await service.CheckArrInstanceHealthAsync(missing);

        status.IsHealthy.ShouldBeFalse();
        status.ErrorMessage.ShouldBe("Arr instance not found in configuration");
        service.GetArrInstanceHealth(missing).ShouldBe(status);
    }

    [Fact]
    public async Task An_arr_probe_that_throws_reports_unhealthy_with_the_reason()
    {
        SeedArr(InstanceType.Radarr, "main", enabled: true);
        ArrProbeThrows(new InvalidOperationException("api key rejected"));

        ArrHealthStatus status = await BuildService().CheckArrInstanceHealthAsync(Seeded("main"));

        status.IsHealthy.ShouldBeFalse();
        status.ErrorMessage.ShouldBe("Error: api key rejected");
    }

    #endregion

    #region Arr sweep

    [Fact]
    public async Task A_disabled_arr_instance_is_never_probed()
    {
        SeedArr(InstanceType.Sonarr, "enabled", enabled: true);
        SeedArr(InstanceType.Radarr, "disabled", enabled: false);

        IDictionary<Guid, ArrHealthStatus> results = await BuildService().CheckAllArrInstancesHealthAsync();

        results.Keys.ShouldBe([Seeded("enabled")]);
    }

    [Fact]
    public async Task One_failing_arr_instance_does_not_stop_the_sweep()
    {
        SeedArr(InstanceType.Sonarr, "good", enabled: true);
        SeedArr(InstanceType.Radarr, "bad", enabled: true);
        ArrProbeThrows(new InvalidOperationException("unreachable"), forInstanceNamed: "bad");

        IDictionary<Guid, ArrHealthStatus> results = await BuildService().CheckAllArrInstancesHealthAsync();

        results.Count.ShouldBe(2);
        results[Seeded("good")].IsHealthy.ShouldBeTrue();
        results[Seeded("bad")].IsHealthy.ShouldBeFalse();
        results[Seeded("bad")].ErrorMessage.ShouldBe("Error: unreachable");
    }

    [Fact]
    public async Task A_deleted_arr_instance_stops_being_reported()
    {
        SeedArr(InstanceType.Sonarr, "removed", enabled: true);
        SeedArr(InstanceType.Radarr, "kept", enabled: true);

        HealthCheckService service = BuildService();
        await service.CheckAllArrInstancesHealthAsync();

        Guid removedId = Seeded("removed");
        await using (DataContext context = new(_options))
        {
            ArrConfig config = await context.ArrConfigs
                .Include(c => c.Instances)
                .FirstAsync(c => c.Instances.Any(i => i.Id == removedId));
            config.Instances.Remove(config.Instances.First(i => i.Id == removedId));
            await context.SaveChangesAsync();
        }

        await service.CheckAllArrInstancesHealthAsync();

        service.GetAllArrInstanceHealth().Keys.ShouldBe([Seeded("kept")]);
        service.GetArrInstanceHealth(removedId).ShouldBeNull();
    }

    [Fact]
    public async Task A_failed_arr_sweep_keeps_what_was_already_cached()
    {
        SeedArr(InstanceType.Sonarr, "main", enabled: true);

        HealthCheckService service = BuildService();
        await service.CheckAllArrInstancesHealthAsync();

        _connection.Close();
        IDictionary<Guid, ArrHealthStatus> results = await service.CheckAllArrInstancesHealthAsync();

        results.ShouldBeEmpty();
        service.GetAllArrInstanceHealth().Count.ShouldBe(1);
    }

    #endregion

    private Guid Seeded(string name) => _seededIds[name];

    private void ProbeReturns(HealthCheckResult result, string? forClientNamed = null)
    {
        IDownloadService downloadService = Substitute.For<IDownloadService>();
        downloadService.HealthCheckAsync().Returns(result);
        RegisterDownloadService(downloadService, forClientNamed);
    }

    private void ProbeThrows(Exception exception, string? forClientNamed = null)
    {
        IDownloadService downloadService = Substitute.For<IDownloadService>();
        downloadService.HealthCheckAsync().Returns(Task.FromException<HealthCheckResult>(exception));
        RegisterDownloadService(downloadService, forClientNamed);
    }

    private void RegisterDownloadService(IDownloadService downloadService, string? forClientNamed)
    {
        _downloadServiceFactory
            .GetDownloadService(forClientNamed is null
                ? Arg.Any<DownloadClientConfig>()
                : Arg.Is<DownloadClientConfig>(c => c.Name == forClientNamed))
            .Returns(downloadService);
    }

    private void ArrProbeThrows(Exception exception, string? forInstanceNamed = null)
    {
        _instanceHealthChecker
            .CheckAsync(
                Arg.Any<InstanceType>(),
                forInstanceNamed is null
                    ? Arg.Any<ArrInstance>()
                    : Arg.Is<ArrInstance>(i => i.Name == forInstanceNamed))
            .Returns(Task.FromException(exception));
    }

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

    private void SeedArr(InstanceType type, string instanceName, bool enabled)
    {
        ArrInstance instance = new()
        {
            Id = Guid.NewGuid(),
            Enabled = enabled,
            Name = instanceName,
            Url = new Uri("http://localhost:8989"),
            ApiKey = "key",
        };

        _seededIds[instanceName] = instance.Id;

        using DataContext context = new(_options);
        context.ArrConfigs.Add(new ArrConfig { Id = Guid.NewGuid(), Type = type, Instances = [instance] });
        context.SaveChanges();
    }

    private async Task DeleteClientAsync(Guid clientId)
    {
        await using DataContext context = new(_options);
        context.DownloadClients.Remove(await context.DownloadClients.FirstAsync(c => c.Id == clientId));
        await context.SaveChangesAsync();
    }

    private HealthCheckService BuildService()
    {
        ServiceCollection services = new();
        services.AddScoped(_ => new DataContext(_options));
        services.AddScoped(_ => _downloadServiceFactory);
        services.AddScoped(_ => _instanceHealthChecker);

        return new HealthCheckService(
            NullLogger<HealthCheckService>.Instance,
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}

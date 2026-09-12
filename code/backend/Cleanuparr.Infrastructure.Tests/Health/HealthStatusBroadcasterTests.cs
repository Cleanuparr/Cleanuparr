using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Infrastructure.Realtime;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Health;

public sealed class HealthStatusBroadcasterTests
{
    private readonly IHealthCheckService _healthCheckService = Substitute.For<IHealthCheckService>();
    private readonly IHealthNotifier _healthNotifier = Substitute.For<IHealthNotifier>();

    [Fact]
    public async Task A_dropped_client_is_broadcast_to_every_connection()
    {
        Guid clientId = Guid.NewGuid();

        HealthStatusBroadcaster broadcaster = BuildBroadcaster();
        await broadcaster.StartAsync(CancellationToken.None);

        _healthCheckService.ClientHealthRemoved += Raise.EventWith(new ClientHealthRemovedEventArgs(clientId));

        await _healthNotifier.Received(1).NotifyClientRemovedAsync(clientId);
    }

    [Fact]
    public async Task Stopping_unsubscribes_from_removals()
    {
        HealthStatusBroadcaster broadcaster = BuildBroadcaster();
        await broadcaster.StartAsync(CancellationToken.None);
        await broadcaster.StopAsync(CancellationToken.None);

        _healthCheckService.ClientHealthRemoved += Raise.EventWith(new ClientHealthRemovedEventArgs(Guid.NewGuid()));

        await _healthNotifier.DidNotReceive().NotifyClientRemovedAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task A_dropped_arr_instance_is_broadcast_to_every_connection()
    {
        Guid instanceId = Guid.NewGuid();

        HealthStatusBroadcaster broadcaster = BuildBroadcaster();
        await broadcaster.StartAsync(CancellationToken.None);

        _healthCheckService.ArrInstanceHealthRemoved += Raise.EventWith(new ArrInstanceHealthRemovedEventArgs(instanceId));

        await _healthNotifier.Received(1).NotifyArrInstanceRemovedAsync(instanceId);
    }

    [Fact]
    public async Task Stopping_unsubscribes_from_arr_removals()
    {
        HealthStatusBroadcaster broadcaster = BuildBroadcaster();
        await broadcaster.StartAsync(CancellationToken.None);
        await broadcaster.StopAsync(CancellationToken.None);

        _healthCheckService.ArrInstanceHealthRemoved += Raise.EventWith(new ArrInstanceHealthRemovedEventArgs(Guid.NewGuid()));

        await _healthNotifier.DidNotReceive().NotifyArrInstanceRemovedAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task A_first_sweep_of_a_healthy_client_only_broadcasts_the_status()
    {
        HealthStatus status = BuildStatus(isHealthy: true);

        HealthStatusBroadcaster broadcaster = BuildBroadcaster();
        await broadcaster.StartAsync(CancellationToken.None);

        _healthCheckService.ClientHealthChanged +=
            Raise.EventWith(new ClientHealthChangedEventArgs(status.ClientId, status, previousStatus: null));

        await _healthNotifier.Received(1).NotifyHealthStatusChangedAsync(status);
        await _healthNotifier.DidNotReceive().NotifyClientDegradedAsync(Arg.Any<HealthStatus>());
        await _healthNotifier.DidNotReceive().NotifyClientRecoveredAsync(Arg.Any<HealthStatus>());
    }

    [Fact]
    public async Task A_client_turning_unhealthy_is_broadcast_as_degraded()
    {
        HealthStatus status = BuildStatus(isHealthy: false);

        HealthStatusBroadcaster broadcaster = BuildBroadcaster();
        await broadcaster.StartAsync(CancellationToken.None);

        _healthCheckService.ClientHealthChanged +=
            Raise.EventWith(new ClientHealthChangedEventArgs(status.ClientId, status, BuildStatus(isHealthy: true)));

        await _healthNotifier.Received(1).NotifyHealthStatusChangedAsync(status);
        await _healthNotifier.Received(1).NotifyClientDegradedAsync(status);
        await _healthNotifier.DidNotReceive().NotifyClientRecoveredAsync(Arg.Any<HealthStatus>());
    }

    [Fact]
    public async Task A_client_turning_healthy_again_is_broadcast_as_recovered()
    {
        HealthStatus status = BuildStatus(isHealthy: true);

        HealthStatusBroadcaster broadcaster = BuildBroadcaster();
        await broadcaster.StartAsync(CancellationToken.None);

        _healthCheckService.ClientHealthChanged +=
            Raise.EventWith(new ClientHealthChangedEventArgs(status.ClientId, status, BuildStatus(isHealthy: false)));

        await _healthNotifier.Received(1).NotifyHealthStatusChangedAsync(status);
        await _healthNotifier.Received(1).NotifyClientRecoveredAsync(status);
        await _healthNotifier.DidNotReceive().NotifyClientDegradedAsync(Arg.Any<HealthStatus>());
    }

    [Fact]
    public async Task Stopping_unsubscribes_from_health_changes()
    {
        HealthStatusBroadcaster broadcaster = BuildBroadcaster();
        await broadcaster.StartAsync(CancellationToken.None);
        await broadcaster.StopAsync(CancellationToken.None);

        HealthStatus status = BuildStatus(isHealthy: false);
        _healthCheckService.ClientHealthChanged +=
            Raise.EventWith(new ClientHealthChangedEventArgs(status.ClientId, status, previousStatus: null));

        await _healthNotifier.DidNotReceive().NotifyHealthStatusChangedAsync(Arg.Any<HealthStatus>());
    }

    private static HealthStatus BuildStatus(bool isHealthy) => new()
    {
        ClientId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        ClientName = "qbit",
        IsHealthy = isHealthy,
        LastChecked = DateTimeOffset.UnixEpoch,
    };

    private HealthStatusBroadcaster BuildBroadcaster() =>
        new(NullLogger<HealthStatusBroadcaster>.Instance, _healthCheckService, _healthNotifier);
}

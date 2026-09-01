using Cleanuparr.Infrastructure.Health;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Health;

public sealed class HealthStatusBroadcasterTests
{
    private readonly IHealthCheckService _healthCheckService = Substitute.For<IHealthCheckService>();
    private readonly IHubContext<HealthStatusHub> _hubContext = Substitute.For<IHubContext<HealthStatusHub>>();
    private readonly IClientProxy _allClients = Substitute.For<IClientProxy>();

    public HealthStatusBroadcasterTests()
    {
        IHubClients clients = Substitute.For<IHubClients>();
        clients.All.Returns(_allClients);
        _hubContext.Clients.Returns(clients);
        _allClients
            .SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task A_dropped_client_is_broadcast_to_every_connection()
    {
        Guid clientId = Guid.NewGuid();

        HealthStatusBroadcaster broadcaster = BuildBroadcaster();
        await broadcaster.StartAsync(CancellationToken.None);

        _healthCheckService.ClientHealthRemoved += Raise.EventWith(new ClientHealthRemovedEventArgs(clientId));

        await _allClients.Received(1).SendCoreAsync(
            "ClientRemoved",
            Arg.Is<object?[]>(args => args.Length == 1 && Equals(args[0], clientId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stopping_unsubscribes_from_removals()
    {
        HealthStatusBroadcaster broadcaster = BuildBroadcaster();
        await broadcaster.StartAsync(CancellationToken.None);
        await broadcaster.StopAsync(CancellationToken.None);

        _healthCheckService.ClientHealthRemoved += Raise.EventWith(new ClientHealthRemovedEventArgs(Guid.NewGuid()));

        await _allClients.DidNotReceive().SendCoreAsync(
            "ClientRemoved",
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }

    private HealthStatusBroadcaster BuildBroadcaster() =>
        new(NullLogger<HealthStatusBroadcaster>.Instance, _healthCheckService, _hubContext);
}

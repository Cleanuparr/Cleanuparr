using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace Cleanuparr.Api.Tests.Hubs;

/// <summary>
/// A hub context substitute that records what the adapters broadcast.
/// </summary>
public sealed class HubMessage<THub>
    where THub : Hub
{
    private readonly List<(string Method, object? Payload)> _sends = [];

    public IHubContext<THub> Context { get; }

    public HubMessage()
    {
        Context = Substitute.For<IHubContext<THub>>();
        IClientProxy proxy = Substitute.For<IClientProxy>();
        IHubClients clients = Substitute.For<IHubClients>();
        clients.All.Returns(proxy);
        Context.Clients.Returns(clients);

        proxy
            .SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                object?[] args = callInfo.ArgAt<object?[]>(1);
                _sends.Add((callInfo.ArgAt<string>(0), args.Length == 0 ? null : args[0]));
                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// The single message broadcast so far, failing when there was not exactly one.
    /// </summary>
    public (string Method, object? Payload) Single()
    {
        if (_sends.Count != 1)
        {
            throw new InvalidOperationException($"Expected one broadcast, got {_sends.Count}.");
        }

        return _sends[0];
    }
}

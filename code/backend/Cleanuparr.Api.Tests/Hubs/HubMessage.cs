using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace Cleanuparr.Api.Tests.Hubs;

/// <summary>
/// A hub context substitute that records what the adapters broadcast.
/// </summary>
public sealed class HubMessage<THub>
    where THub : Hub
{
    private readonly List<(string Method, object?[] Arguments)> _sends = [];

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
                _sends.Add((callInfo.ArgAt<string>(0), callInfo.ArgAt<object?[]>(1)));
                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// The single message broadcast so far, failing unless it carried exactly one argument.
    /// </summary>
    public (string Method, object? Payload) Single()
    {
        (string method, object?[] arguments) = SingleSend();

        if (arguments.Length != 1)
        {
            throw new InvalidOperationException($"Expected one argument, got {arguments.Length}.");
        }

        return (method, arguments[0]);
    }

    /// <summary>
    /// The name of the single message broadcast so far, failing unless it carried no arguments.
    /// </summary>
    public string SingleWithoutPayload()
    {
        (string method, object?[] arguments) = SingleSend();

        if (arguments.Length != 0)
        {
            throw new InvalidOperationException($"Expected no arguments, got {arguments.Length}.");
        }

        return method;
    }

    private (string Method, object?[] Arguments) SingleSend()
    {
        if (_sends.Count != 1)
        {
            throw new InvalidOperationException($"Expected one broadcast, got {_sends.Count}.");
        }

        return _sends[0];
    }
}

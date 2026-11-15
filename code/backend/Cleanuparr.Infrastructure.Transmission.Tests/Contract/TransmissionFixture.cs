using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Transmission.API.RPC;
using Xunit;

namespace Cleanuparr.Infrastructure.Transmission.Tests.Contract;

/// <summary>
/// Runs one real Transmission container for a test class and keeps an authenticated client on it.
/// </summary>
public abstract class TransmissionFixture : IAsyncLifetime
{
    private const int RpcPort = 9091;
    private const string Username = "admin";
    private const string Password = "adminadmin";

    private readonly string _image;
    private IContainer? _container;
    private readonly List<HttpClient> _httpClients = [];

    protected TransmissionFixture(string image)
    {
        _image = image;
    }

    public string? UnavailableReason { get; private set; }

    public string RpcUrl { get; private set; } = string.Empty;

    public Client Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new ContainerBuilder(_image)
                .WithPortBinding(RpcPort, true)
                .WithEnvironment("PUID", "1000")
                .WithEnvironment("PGID", "1000")
                .WithEnvironment("USER", Username)
                .WithEnvironment("PASS", Password)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
                    .ForPort(RpcPort)
                    .ForPath("/transmission/rpc")
                    .ForStatusCodeMatching(_ => true)))
                .Build();

            await _container.StartAsync();

            RpcUrl = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(RpcPort)}/transmission/rpc";
            Client = CreateClient(Username, Password);
        }
        catch (Exception exception)
        {
            UnavailableReason = $"Docker or {_image} is unavailable: {exception.Message}";
        }
    }

    public async Task DisposeAsync()
    {
        foreach (HttpClient httpClient in _httpClients)
        {
            httpClient.Dispose();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public Client CreateClient(string? login, string? password)
    {
        HttpClient httpClient = new();
        _httpClients.Add(httpClient);
        return new Client(httpClient, RpcUrl, login: login, password: password);
    }
}

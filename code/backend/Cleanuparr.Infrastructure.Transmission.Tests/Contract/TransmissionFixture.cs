using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Transmission.API.RPC;
using Transmission.API.RPC.Entity;
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
        catch (DockerUnavailableException exception)
        {
            UnavailableReason = $"Docker is unavailable: {exception.Message}";
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

    /// <summary>
    /// Replaces every torrent with the seed torrent, added paused, and returns its id and hash.
    /// </summary>
    public async Task<(long Id, string Hash)> ResetWithSeedTorrentAsync()
    {
        TransmissionTorrents? existing = await Client.TorrentGetAsync([TorrentFields.ID]);
        long[] existingIds = existing?.Torrents?.Select(torrent => torrent.Id).ToArray() ?? [];
        if (existingIds.Length > 0)
        {
            await Client.TorrentRemoveAsync(existingIds, true);
        }

        byte[] metainfo = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Contract", "seed.torrent"));
        NewTorrentInfo? added = await Client.TorrentAddAsync(new NewTorrent
        {
            Metainfo = Convert.ToBase64String(metainfo),
            Paused = true,
        });

        string hash = added?.HashString ?? throw new InvalidOperationException("The seed torrent was not added.");

        for (int attempt = 0; attempt < 50; attempt++)
        {
            TransmissionTorrents? torrents = await Client.TorrentGetAsync([TorrentFields.ID, TorrentFields.STATUS], hash);
            if (torrents?.Torrents?.Length == 1 && torrents.Torrents[0].Status == 0)
            {
                return (torrents.Torrents[0].Id, hash);
            }

            await Task.Delay(200);
        }

        throw new InvalidOperationException("The seed torrent never appeared in the torrent list.");
    }
}

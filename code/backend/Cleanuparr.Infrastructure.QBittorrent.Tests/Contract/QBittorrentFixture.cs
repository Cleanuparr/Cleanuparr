using System.Net;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using QBittorrent.Client;
using Xunit;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests.Contract;

/// <summary>
/// Runs one real qBittorrent container for a test class and keeps a logged in client on it.
/// </summary>
public abstract class QBittorrentFixture : IAsyncLifetime
{
    private const int WebUiPort = 8080;
    private const string Username = "admin";
    private const string Password = "adminadmin";

    private readonly string _image;
    private IContainer? _container;
    private HttpClient? _http;

    protected QBittorrentFixture(string image)
    {
        _image = image;
    }

    public string? UnavailableReason { get; private set; }

    public Uri BaseUri { get; private set; } = null!;

    public HttpClient Http => _http!;

    public QBittorrentClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            string directory = AppContext.BaseDirectory;
            byte[] config = await File.ReadAllBytesAsync(Path.Combine(directory, "Contract", "qBittorrent.conf"));

            _container = new ContainerBuilder(_image)
                .WithPortBinding(WebUiPort, true)
                .WithEnvironment("PUID", "1000")
                .WithEnvironment("PGID", "1000")
                .WithEnvironment("WEBUI_PORT", WebUiPort.ToString())
                .WithResourceMapping(config, "/config/qBittorrent/qBittorrent.conf")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
                    .ForPort(WebUiPort)
                    .ForPath("/")
                    .ForStatusCodeMatching(_ => true)))
                .Build();

            await _container.StartAsync();

            BaseUri = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(WebUiPort)}/");
            _http = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() });
            Client = new QBittorrentClient(_http, BaseUri);
            await Client.LoginAsync(Username, Password);
        }
        catch (Exception exception)
        {
            UnavailableReason = $"Docker or {_image} is unavailable: {exception.Message}";
        }
    }

    public async Task DisposeAsync()
    {
        _http?.Dispose();

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public QBittorrentClient CreateUnauthenticatedClient()
    {
        return new QBittorrentClient(new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }), BaseUri);
    }

    /// <summary>
    /// Replaces every torrent with the seed torrent, added stopped, and returns its hash.
    /// </summary>
    public async Task<string> ResetWithSeedTorrentAsync()
    {
        IReadOnlyList<TorrentInfo> existing = await Client.GetTorrentListAsync() ?? [];
        if (existing.Count > 0)
        {
            await Client.DeleteAsync(existing.Select(torrent => torrent.Hash), true);
        }

        using MultipartFormDataContent form = new();
        form.Add(new ByteArrayContent(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Contract", "seed.torrent"))), "torrents", "seed.torrent");
        // 4.x and 5.x name the field differently
        form.Add(new StringContent("true"), "paused");
        form.Add(new StringContent("false"), "stopped");

        HttpResponseMessage response = await Http.PostAsync(new Uri(BaseUri, "api/v2/torrents/add"), form);
        response.EnsureSuccessStatusCode();

        for (int attempt = 0; attempt < 50; attempt++)
        {
            IReadOnlyList<TorrentInfo> torrents = await Client.GetTorrentListAsync() ?? [];
            if (torrents.Count == 1)
            {
                return torrents[0].Hash;
            }

            await Task.Delay(200);
        }

        throw new InvalidOperationException("The seed torrent never appeared in the torrent list.");
    }
}

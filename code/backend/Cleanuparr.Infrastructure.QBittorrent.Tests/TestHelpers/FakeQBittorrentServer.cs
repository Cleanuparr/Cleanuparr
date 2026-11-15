using System.Net;
using System.Text;
using QBittorrent.Client;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests.TestHelpers;

/// <summary>
/// Routes requests by path onto canned responses and builds a client on top of them.
/// </summary>
public sealed class FakeQBittorrentServer : IDisposable
{
    public static readonly Uri BaseUri = new("http://localhost:8080/");

    private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _routes = new();

    public FakeQBittorrentServer(string webApiVersion = "2.11.4")
    {
        Handler = new FakeHttpMessageHandler();
        Handler.SetupResponse(Respond);
        Client = new QBittorrentClient(new HttpClient(Handler), BaseUri);

        // qBittorrent 4.2+ has no legacy version endpoint
        Route("version/api", "", HttpStatusCode.NotFound);
        Route("api/v2/app/webapiVersion", webApiVersion);
    }

    public FakeHttpMessageHandler Handler { get; }

    public QBittorrentClient Client { get; }

    /// <summary>
    /// Requests that reach the API, without the version probes every call starts with.
    /// </summary>
    public IReadOnlyList<RecordedRequest> ApiRequests => Handler.Requests
        .Where(request => request.Uri.AbsolutePath != "/version/api"
                          && request.Uri.AbsolutePath != "/api/v2/app/webapiVersion")
        .ToList();

    public RecordedRequest SingleApiRequest => ApiRequests.Single();

    public void Route(string path, string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes[path] = (status, body);
    }

    public void Dispose()
    {
        Client.Dispose();
    }

    private HttpResponseMessage Respond(HttpRequestMessage request)
    {
        string path = request.RequestUri!.AbsolutePath.TrimStart('/');
        if (_routes.TryGetValue(path, out (HttpStatusCode Status, string Body) route))
        {
            return new HttpResponseMessage(route.Status)
            {
                Content = new StringContent(route.Body, Encoding.UTF8)
            };
        }

        // writes succeed with an empty body unless a test routes them elsewhere
        return new HttpResponseMessage(request.Method == HttpMethod.Post ? HttpStatusCode.OK : HttpStatusCode.NotFound);
    }
}

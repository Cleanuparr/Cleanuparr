using System.Net;
using System.Text;
using Transmission.API.RPC;

namespace Cleanuparr.Infrastructure.Transmission.Tests.TestHelpers;

public sealed class RpcTestClient
{
    public FakeHttpMessageHandler Handler { get; } = new();

    public Client Client { get; }

    public RpcTestClient(string argumentsJson = "{}", string? login = null, string? password = null)
    {
        Handler.SetupResponse(_ => Json(HttpStatusCode.OK, Envelope("success", argumentsJson)));
        Client = new Client(new HttpClient(Handler), "http://localhost:9091/transmission/rpc", sessionID: "session", login: login, password: password);
    }

    public RecordedRequest SingleRequest => Handler.Requests.Single();

    public static string Envelope(string result, string argumentsJson) =>
        $$"""{"arguments":{{argumentsJson}},"result":"{{result}}","tag":1}""";

    public static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

using System.Net;
using Cleanuparr.Infrastructure.Services;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.General;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Services;

public class ConnectivityCheckerTests
{
    private readonly ILogger<ConnectivityChecker> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FakeHttpMessageHandler _httpHandler;

    public ConnectivityCheckerTests()
    {
        _logger = Substitute.For<ILogger<ConnectivityChecker>>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _httpHandler = new FakeHttpMessageHandler();
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(_httpHandler));
    }

    private ConnectivityChecker CreateChecker() => new(_logger, _httpClientFactory);

    [Fact]
    public async Task IsOnlineAsync_Disabled_ReturnsTrueWithoutProbing()
    {
        GeneralConfig config = new()
        {
            ConnectivityCheckEnabled = false,
            ConnectivityCheckUrls = ["https://example.com"],
        };

        bool result = await CreateChecker().IsOnlineAsync(config);

        result.ShouldBeTrue();
        _httpHandler.CapturedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task IsOnlineAsync_EnabledWithNoUrls_ReturnsTrueWithoutProbing()
    {
        GeneralConfig config = new()
        {
            ConnectivityCheckEnabled = true,
            ConnectivityCheckUrls = [],
        };

        bool result = await CreateChecker().IsOnlineAsync(config);

        result.ShouldBeTrue();
        _httpHandler.CapturedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task IsOnlineAsync_SuccessResponse_ReturnsTrue()
    {
        _httpHandler.SetupResponse(HttpStatusCode.OK);
        GeneralConfig config = new()
        {
            ConnectivityCheckEnabled = true,
            ConnectivityCheckUrls = ["https://example.com"],
        };

        bool result = await CreateChecker().IsOnlineAsync(config);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsOnlineAsync_AllRequestsThrow_ReturnsFalse()
    {
        _httpHandler.SetupThrow(new HttpRequestException("no route to host"));
        GeneralConfig config = new()
        {
            ConnectivityCheckEnabled = true,
            ConnectivityCheckUrls = ["https://a.example", "https://b.example"],
        };

        bool result = await CreateChecker().IsOnlineAsync(config);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsOnlineAsync_AllNonSuccessStatus_ReturnsFalse()
    {
        _httpHandler.SetupResponse(HttpStatusCode.InternalServerError);
        GeneralConfig config = new()
        {
            ConnectivityCheckEnabled = true,
            ConnectivityCheckUrls = ["https://gluetun:9999"],
        };

        bool result = await CreateChecker().IsOnlineAsync(config);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsOnlineAsync_CallerCancelled_ThrowsInsteadOfReportingOffline()
    {
        _httpHandler.SetupResponse(HttpStatusCode.OK);
        GeneralConfig config = new()
        {
            ConnectivityCheckEnabled = true,
            ConnectivityCheckUrls = ["https://example.com"],
        };
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => CreateChecker().IsOnlineAsync(config, cts.Token));
    }

    [Fact]
    public async Task IsOnlineAsync_FirstFailsSecondSucceeds_ReturnsTrue()
    {
        _httpHandler.SetupResponse((request, _) =>
        {
            HttpStatusCode status = request.RequestUri!.Host == "up.example"
                ? HttpStatusCode.OK
                : HttpStatusCode.ServiceUnavailable;
            return Task.FromResult(new HttpResponseMessage(status));
        });
        GeneralConfig config = new()
        {
            ConnectivityCheckEnabled = true,
            ConnectivityCheckUrls = ["https://down.example", "https://up.example"],
        };

        bool result = await CreateChecker().IsOnlineAsync(config);

        result.ShouldBeTrue();
    }
}

using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cleanuparr.Infrastructure.Tests.Features.Arr;

public class WhisparrV3ClientTests
{
    private readonly ILogger<WhisparrV3Client> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IStriker _striker;
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly FakeHttpMessageHandler _httpMessageHandler;
    private readonly WhisparrV3Client _client;

    public WhisparrV3ClientTests()
    {
        _logger = Substitute.For<ILogger<WhisparrV3Client>>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _striker = Substitute.For<IStriker>();
        _dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        _httpMessageHandler = new FakeHttpMessageHandler();

        var httpClient = new HttpClient(_httpMessageHandler);
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        _client = new WhisparrV3Client(
            _logger,
            _httpClientFactory,
            _striker,
            _dryRunInterceptor
        );
    }
}

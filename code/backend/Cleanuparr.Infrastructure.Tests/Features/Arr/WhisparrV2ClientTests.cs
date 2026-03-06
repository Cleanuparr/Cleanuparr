using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cleanuparr.Infrastructure.Tests.Features.Arr;

public class WhisparrV2ClientTests
{
    private readonly Mock<ILogger<WhisparrV2Client>> _loggerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IStriker> _strikerMock;
    private readonly Mock<IDryRunInterceptor> _dryRunInterceptorMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly WhisparrV2Client _client;

    public WhisparrV2ClientTests()
    {
        _loggerMock = new Mock<ILogger<WhisparrV2Client>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _strikerMock = new Mock<IStriker>();
        _dryRunInterceptorMock = new Mock<IDryRunInterceptor>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        _client = new WhisparrV2Client(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _strikerMock.Object,
            _dryRunInterceptorMock.Object
        );
    }
}

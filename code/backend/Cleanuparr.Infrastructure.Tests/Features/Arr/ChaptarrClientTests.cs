using System.Net;
using System.Text;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Arr;

public class ChaptarrClientTests
{
    private readonly FakeHttpMessageHandler _httpMessageHandler;
    private readonly ChaptarrClient _client;
    private readonly ArrInstance _arrInstance;

    public ChaptarrClientTests()
    {
        ILogger<ChaptarrClient> logger = Substitute.For<ILogger<ChaptarrClient>>();
        IStriker striker = Substitute.For<IStriker>();
        IDryRunInterceptor dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        _httpMessageHandler = new FakeHttpMessageHandler();

        HttpClient httpClient = new(_httpMessageHandler);
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        _client = new ChaptarrClient(logger, httpClientFactory, striker, dryRunInterceptor);
        _arrInstance = new ArrInstance
        {
            Name = "chaptarr",
            Url = new Uri("http://localhost:8789/"),
            ApiKey = "api-key",
        };
    }

    [Fact]
    public async Task GetAllTagsAsync_DeserializesListFromV1Path()
    {
        // Arrange
        const string body = """[{"id":3,"label":"epub"}]""";
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }));

        // Act
        List<Tag> tags = await _client.GetAllTagsAsync(_arrInstance);

        // Assert
        Tag tag = tags.ShouldHaveSingleItem();
        tag.Id.ShouldBe(3);
        tag.Label.ShouldBe("epub");
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/tag");
    }
}

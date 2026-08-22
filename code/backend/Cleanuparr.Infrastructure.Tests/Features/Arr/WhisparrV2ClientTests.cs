using System.Net;
using System.Text;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Arr;

public class WhisparrV2ClientTests
{
    private readonly ILogger<WhisparrV2Client> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IStriker _striker;
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly FakeHttpMessageHandler _httpMessageHandler;
    private readonly WhisparrV2Client _client;
    private readonly ArrInstance _arrInstance;

    public WhisparrV2ClientTests()
    {
        _logger = Substitute.For<ILogger<WhisparrV2Client>>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _striker = Substitute.For<IStriker>();
        _dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        _httpMessageHandler = new FakeHttpMessageHandler();

        var httpClient = new HttpClient(_httpMessageHandler);
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        _client = new WhisparrV2Client(
            _logger,
            _httpClientFactory,
            _striker,
            _dryRunInterceptor
        );
        _arrInstance = new ArrInstance
        {
            Name = "whisparr",
            Url = new Uri("http://localhost:6969/"),
            ApiKey = "api-key",
        };

        _dryRunInterceptor.IsDryRunEnabled().Returns(false);
        _dryRunInterceptor
            .InterceptAsync<HttpResponseMessage>(Arg.Any<Func<Task<HttpResponseMessage>>>(), Arg.Any<string?>())
            .Returns(async ci =>
            {
                Func<Task<HttpResponseMessage>> action = ci.Arg<Func<Task<HttpResponseMessage>>>();
                return await action();
            });
    }

    #region GetSearchCommands

    [Fact]
    public async Task SearchItemsAsync_MultipleEpisodes_BundlesIntoSingleCommand()
    {
        // Arrange
        RouteResponses();
        var items = new HashSet<SearchItem>
        {
            new SeriesSearchItem { Id = 1, SeriesId = 10, SearchType = SeriesSearchType.Episode },
            new SeriesSearchItem { Id = 2, SeriesId = 10, SearchType = SeriesSearchType.Episode },
        };

        // Act
        await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        var posts = _httpMessageHandler.CapturedRequests.Where(r => r.Method == HttpMethod.Post).ToList();
        posts.Count.ShouldBe(1);
        var body = _httpMessageHandler.CapturedRequestBodies[_httpMessageHandler.CapturedRequests.IndexOf(posts[0])];
        body.ShouldNotBeNull();
        body!.ShouldContain("\"name\":\"EpisodeSearch\"", Case.Insensitive);
        body!.ShouldContain("\"episodeIds\":[1,2]", Case.Insensitive);
    }

    [Fact]
    public async Task SearchItemsAsync_SeriesThenEpisode_PostsBothCommands()
    {
        // Arrange
        RouteResponses();
        var items = new HashSet<SearchItem>
        {
            new SeriesSearchItem { Id = 10, SeriesId = 10, SearchType = SeriesSearchType.Series },
            new SeriesSearchItem { Id = 1, SeriesId = 10, SearchType = SeriesSearchType.Episode },
        };

        // Act
        await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        var posts = _httpMessageHandler.CapturedRequests.Where(r => r.Method == HttpMethod.Post).ToList();
        posts.Count.ShouldBe(2);
        var bodies = posts
            .Select(p => _httpMessageHandler.CapturedRequestBodies[_httpMessageHandler.CapturedRequests.IndexOf(p)])
            .ToList();
        bodies.ShouldContain(b => b != null && b.Contains("\"name\":\"SeriesSearch\"", StringComparison.OrdinalIgnoreCase) && !b.Contains("episodeIds", StringComparison.OrdinalIgnoreCase));
        bodies.ShouldContain(b => b != null && b.Contains("\"name\":\"EpisodeSearch\"", StringComparison.OrdinalIgnoreCase) && b.Contains("\"episodeIds\":[1]", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchItemsAsync_EpisodesAroundOtherTypes_BundlesEpisodesIntoSingleCommand()
    {
        // Arrange
        RouteResponses();
        var items = new HashSet<SearchItem>
        {
            new SeriesSearchItem { Id = 1, SeriesId = 10, SearchType = SeriesSearchType.Episode },
            new SeriesSearchItem { Id = 4, SeriesId = 10, SearchType = SeriesSearchType.Season },
            new SeriesSearchItem { Id = 2, SeriesId = 10, SearchType = SeriesSearchType.Episode },
        };

        // Act
        await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        var posts = _httpMessageHandler.CapturedRequests.Where(r => r.Method == HttpMethod.Post).ToList();
        posts.Count.ShouldBe(2);
        var episodeBody = posts
            .Select(p => _httpMessageHandler.CapturedRequestBodies[_httpMessageHandler.CapturedRequests.IndexOf(p)])
            .Single(b => b != null && b.Contains("\"name\":\"EpisodeSearch\"", StringComparison.OrdinalIgnoreCase));
        episodeBody!.ShouldContain("\"episodeIds\":[1,2]", Case.Insensitive);
    }

    #endregion

    #region Helpers

    private void RouteResponses()
    {
        _httpMessageHandler.SetupResponse((req, _) =>
        {
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath.EndsWith("/command"))
            {
                return Task.FromResult(JsonResponse(new { id = 1 }));
            }
            // GET log-context calls (/series/{id}, /episode?...) return null so log context bails out
            return Task.FromResult(JsonNullResponse());
        });
    }

    private static HttpResponseMessage JsonResponse<T>(T body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage JsonNullResponse() => new(HttpStatusCode.OK)
    {
        Content = new StringContent("null", Encoding.UTF8, "application/json"),
    };

    #endregion
}

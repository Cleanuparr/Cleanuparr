using System.Net;
using System.Text;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Entities.Sonarr;
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

/// <summary>
/// SportarrClient inherits nearly all of its behavior from <see cref="SonarrClient"/> (see the
/// class doc comment on SportarrClient.cs) since Sportarr speaks the same wire protocol as
/// Sonarr. That shared behavior — queue URLs, health check, tag/episode/quality-profile
/// fetching, dry-run handling, etc. — is already covered by <see cref="SonarrClientTests"/>
/// against the same code path. These tests only cover what's actually different: the
/// <c>GetSearchCommands</c> override and the fact that DI resolves a working SportarrClient.
/// </summary>
public class SportarrClientTests
{
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly FakeHttpMessageHandler _httpMessageHandler;
    private readonly SportarrClient _client;
    private readonly ArrInstance _arrInstance;

    public SportarrClientTests()
    {
        var logger = Substitute.For<ILogger<SportarrClient>>();
        var striker = Substitute.For<IStriker>();
        _dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        _httpMessageHandler = new FakeHttpMessageHandler();

        var httpClient = new HttpClient(_httpMessageHandler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        _client = new SportarrClient(logger, httpClientFactory, striker, _dryRunInterceptor);
        _arrInstance = new ArrInstance
        {
            Name = "sportarr",
            Url = new Uri("http://localhost:1867/"),
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

    [Fact]
    public async Task HealthCheckAsync_UsesInheritedV3SystemStatus()
    {
        // Arrange
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await _client.HealthCheckAsync(_arrInstance);

        // Assert — proves the SonarrClient base implementation runs correctly for a Sportarr instance
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v3/system/status");
    }

    #region GetSearchCommands override

    [Fact]
    public async Task SearchItemsAsync_SeasonThenEpisode_StillSearchesEpisode()
    {
        // Arrange — regression test: a season item processed before an episode item used to
        // cause the episode search to be merged into the season command and dropped
        // (commands.FirstOrDefault() picked whichever command was first, regardless of type).
        RouteResponses(commandIdForPost: 11);
        var items = new HashSet<SearchItem>
        {
            new SeriesSearchItem { Id = 3, SeriesId = 10, SearchType = SeriesSearchType.Season },
            new SeriesSearchItem { Id = 1, SeriesId = 10, SearchType = SeriesSearchType.Episode },
        };

        // Act
        var ids = await _client.SearchItemsAsync(_arrInstance, items);

        // Assert — two distinct commands should be posted, one per search type
        ids.ShouldBe(new long[] { 11, 11 });
        var posts = _httpMessageHandler.CapturedRequests.Where(r => r.Method == HttpMethod.Post).ToList();
        posts.Count.ShouldBe(2);
        var bodies = posts.Select(p => _httpMessageHandler.CapturedRequestBodies[_httpMessageHandler.CapturedRequests.IndexOf(p)]).ToList();
        bodies.ShouldContain(b => b != null && b.Contains("\"name\":\"SeasonSearch\"", StringComparison.OrdinalIgnoreCase));
        bodies.ShouldContain(b => b != null && b.Contains("\"name\":\"EpisodeSearch\"", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchItemsAsync_SeriesThenEpisode_StillSearchesEpisode()
    {
        // Arrange — same regression, series item ahead of the episode item this time.
        RouteResponses(commandIdForPost: 22);
        var items = new HashSet<SearchItem>
        {
            new SeriesSearchItem { Id = 10, SeriesId = 10, SearchType = SeriesSearchType.Series },
            new SeriesSearchItem { Id = 2, SeriesId = 10, SearchType = SeriesSearchType.Episode },
        };

        // Act
        var ids = await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        ids.ShouldBe(new long[] { 22, 22 });
        var posts = _httpMessageHandler.CapturedRequests.Where(r => r.Method == HttpMethod.Post).ToList();
        posts.Count.ShouldBe(2);
        var bodies = posts.Select(p => _httpMessageHandler.CapturedRequestBodies[_httpMessageHandler.CapturedRequests.IndexOf(p)]).ToList();
        bodies.ShouldContain(b => b != null && b.Contains("\"name\":\"SeriesSearch\"", StringComparison.OrdinalIgnoreCase));
        bodies.ShouldContain(b => b != null && b.Contains("\"name\":\"EpisodeSearch\"", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchItemsAsync_MultipleEpisodesAcrossOtherTypes_BundlesIntoSingleEpisodeCommand()
    {
        // Arrange — a season command sits between two episode items; both episodes must still
        // land in the same, single episode command rather than being split or dropped.
        RouteResponses(commandIdForPost: 33);
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

    private void RouteResponses(long commandIdForPost)
    {
        _httpMessageHandler.SetupResponse((req, _) =>
        {
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath.EndsWith("/command"))
            {
                return Task.FromResult(JsonResponse(new { id = commandIdForPost }));
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

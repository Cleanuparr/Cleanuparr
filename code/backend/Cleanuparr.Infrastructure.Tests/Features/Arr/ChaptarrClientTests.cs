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

    [Fact]
    public async Task GetAllTagsAsync_EmptyBody_ReturnsEmpty()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));

        // Act
        List<Tag> tags = await _client.GetAllTagsAsync(_arrInstance);

        // Assert
        tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task StreamAllAuthorsAsync_DeserializesPerSideFields()
    {
        // Arrange
        const string body = """
            [{"id":7,"authorName":"Author","monitored":true,"audiobookMonitored":true,"ebookMonitored":false,"audiobookTags":[1,2],"ebookTags":[3],"added":"2026-01-02T03:04:05Z"},
             {"id":8,"authorName":"Bare","monitored":false}]
            """;
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(RawJsonResponse(body)));

        // Act
        List<SearchableAuthor> authors = [];
        await foreach (SearchableAuthor author in _client.StreamAllAuthorsAsync(_arrInstance))
        {
            authors.Add(author);
        }

        // Assert
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/author");
        authors.Count.ShouldBe(2);
        authors[0].Id.ShouldBe(7);
        authors[0].AuthorName.ShouldBe("Author");
        authors[0].Monitored.ShouldBeTrue();
        authors[0].AudiobookTags.ShouldBe([1L, 2L]);
        authors[0].EbookTags.ShouldBe([3L]);
        authors[0].Added.ShouldBe(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        authors[1].AudiobookTags.ShouldBeEmpty();
        authors[1].EbookTags.ShouldBeEmpty();
        authors[1].Added.ShouldBeNull();
    }

    [Fact]
    public async Task GetMissingBooksAsync_MonitoredTrue_BuildsExpectedUri()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(RawJsonResponse("""{"records":[]}""")));

        // Act
        await _client.GetMissingBooksAsync(_arrInstance, monitored: true);

        // Assert
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/wanted/missing");
        request.RequestUri.Query.ShouldBe("?page=1&pageSize=1000&monitored=true");
    }

    [Fact]
    public async Task GetCutoffUnmetBooksAsync_MonitoredFalse_BuildsExpectedUri()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(RawJsonResponse("""{"records":[]}""")));

        // Act
        await _client.GetCutoffUnmetBooksAsync(_arrInstance, monitored: false);

        // Assert
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/wanted/cutoff");
        request.RequestUri.Query.ShouldBe("?page=1&pageSize=1000&monitored=false");
    }

    [Fact]
    public async Task GetMissingBooksAsync_DeserializesMediaTypes()
    {
        // Arrange
        const string body = """
            {"page":1,"pageSize":1000,"totalRecords":4,"records":[
              {"id":11,"title":"Book","authorId":7,"monitored":true,"mediaType":"audiobook","releaseDate":"2020-05-06T00:00:00Z"},
              {"id":12,"title":"Book","authorId":7,"monitored":false,"mediaType":"ebook"},
              {"id":13,"title":"Odd","authorId":7,"mediaType":"comic"},
              {"id":14,"title":"Bare","authorId":7}]}
            """;
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(RawJsonResponse(body)));

        // Act
        List<SearchableBook> books = await _client.GetMissingBooksAsync(_arrInstance, monitored: true);

        // Assert
        books.Count.ShouldBe(4);
        books[0].Id.ShouldBe(11);
        books[0].AuthorId.ShouldBe(7);
        books[0].Monitored.ShouldBeTrue();
        books[0].MediaType.ShouldBe(ChaptarrMediaType.Audiobook);
        books[0].ReleaseDate.ShouldBe(new DateTimeOffset(2020, 5, 6, 0, 0, 0, TimeSpan.Zero));
        books[1].MediaType.ShouldBe(ChaptarrMediaType.Ebook);
        books[1].ReleaseDate.ShouldBeNull();
        books[2].MediaType.ShouldBe(ChaptarrMediaType.Unknown);
        books[3].MediaType.ShouldBe(ChaptarrMediaType.Unknown);
    }

    [Fact]
    public async Task GetRecentlyGrabbedBookIdsAsync_BuildsExpectedUri()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(RawJsonResponse("[]")));
        DateTimeOffset since = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        await _client.GetRecentlyGrabbedBookIdsAsync(_arrInstance, since);

        // Assert
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/history/since");
        request.RequestUri.Query.ShouldBe("?date=2026-01-01T00%3A00%3A00.0000000Z&eventType=1");
    }

    [Fact]
    public async Task GetRecentlyGrabbedBookIdsAsync_DropsZeroBookIds()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(RawJsonResponse(
            """[{"bookId":1},{"bookId":0},{"authorId":5}]""")));

        // Act
        HashSet<long> ids = await _client.GetRecentlyGrabbedBookIdsAsync(_arrInstance, DateTimeOffset.UtcNow);

        // Assert
        ids.ShouldBe([1L]);
    }

    [Fact]
    public async Task GetRecentlyGrabbedBookIdsAsync_TheArrRefused_Throws()
    {
        // Arrange
        _httpMessageHandler.SetupResponse(HttpStatusCode.InternalServerError);

        // Act, Assert
        await Should.ThrowAsync<HttpRequestException>(
            () => _client.GetRecentlyGrabbedBookIdsAsync(_arrInstance, DateTimeOffset.UtcNow));
    }

    private static HttpResponseMessage RawJsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
}

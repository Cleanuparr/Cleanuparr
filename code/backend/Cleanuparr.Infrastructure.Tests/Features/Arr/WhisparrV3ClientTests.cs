using Cleanuparr.Domain.Entities.Arr.ManualImport;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

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

    [Fact]
    public void SupportsForceImport_IsTrue()
    {
        _client.SupportsForceImport.ShouldBeTrue();
    }

    [Fact]
    public void MapCandidate_TheMovieMatches_BuildsTheMoviePayload()
    {
        // Arrange
        QueueRecord record = new() { MovieId = 1, DownloadId = "HASH", Title = "movie" };
        ManualImportCandidate candidate = new()
        {
            Path = "/downloads/movie.mkv",
            DownloadId = "HASH",
            ReleaseGroup = "GROUP",
            MovieId = 1,
        };

        // Act
        ManualImportFile? file = _client.MapCandidate(record, candidate);

        // Assert
        file.ShouldNotBeNull();
        file.MovieId.ShouldBe(1);
        file.SeriesId.ShouldBeNull();
        file.ReleaseGroup.ShouldBe("GROUP");
    }

    [Fact]
    public void MapCandidate_NestedMovieMatches_BuildsTheMoviePayload()
    {
        // Arrange: an older build nests the movie
        QueueRecord record = new() { MovieId = 1, DownloadId = "HASH", Title = "movie" };
        ManualImportCandidate candidate = new() { Movie = new ManualImportRef { Id = 1 } };

        // Act, Assert
        _client.MapCandidate(record, candidate)!.MovieId.ShouldBe(1);
    }

    [Fact]
    public void MapCandidate_OtherMovie_ReturnsNull()
    {
        // Arrange
        QueueRecord record = new() { MovieId = 1, DownloadId = "HASH", Title = "movie" };

        // Act, Assert
        _client.MapCandidate(record, new ManualImportCandidate { MovieId = 2 }).ShouldBeNull();
    }

    [Fact]
    public void MapCandidate_NoMovieId_ReturnsNull()
    {
        // Arrange
        QueueRecord record = new() { MovieId = 1, DownloadId = "HASH", Title = "movie" };

        // Act, Assert
        _client.MapCandidate(record, new ManualImportCandidate()).ShouldBeNull();
    }

    [Fact]
    public void HasContentId_NoMovieId_IsFalse()
    {
        _client.HasContentId(new QueueRecord { DownloadId = "HASH", Title = "movie" }).ShouldBeFalse();
    }
}

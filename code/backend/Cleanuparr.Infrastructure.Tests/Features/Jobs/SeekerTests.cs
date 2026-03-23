using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.State;
using Data.Models.Arr;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SeekerJob = Cleanuparr.Infrastructure.Features.Jobs.Seeker;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs;

[Collection(JobHandlerCollection.Name)]
public class SeekerTests : IDisposable
{
    private readonly JobHandlerFixture _fixture;
    private readonly Mock<ILogger<SeekerJob>> _logger;
    private readonly Mock<IRadarrClient> _radarrClient;
    private readonly Mock<ISonarrClient> _sonarrClient;
    private readonly Mock<IDryRunInterceptor> _dryRunInterceptor;
    private readonly Mock<IHostingEnvironment> _hostingEnvironment;

    public SeekerTests(JobHandlerFixture fixture)
    {
        _fixture = fixture;
        _fixture.RecreateDataContext();
        _fixture.ResetMocks();
        _logger = new Mock<ILogger<SeekerJob>>();
        _radarrClient = new Mock<IRadarrClient>();
        _sonarrClient = new Mock<ISonarrClient>();
        _dryRunInterceptor = new Mock<IDryRunInterceptor>();
        _hostingEnvironment = new Mock<IHostingEnvironment>();

        // Default: development mode (skips jitter)
        _hostingEnvironment.Setup(x => x.EnvironmentName).Returns("Development");

        // Default: dry run disabled
        _dryRunInterceptor.Setup(x => x.IsDryRunEnabled()).ReturnsAsync(false);

        // Default: PublishSearchTriggered returns a Guid
        _fixture.EventPublisher
            .Setup(x => x.PublishSearchTriggered(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<SeekerSearchType>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync(Guid.NewGuid());
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private SeekerJob CreateSut()
    {
        return new SeekerJob(
            _logger.Object,
            _fixture.DataContext,
            _radarrClient.Object,
            _sonarrClient.Object,
            _fixture.ArrClientFactory.Object,
            _fixture.EventPublisher.Object,
            _dryRunInterceptor.Object,
            _hostingEnvironment.Object,
            _fixture.TimeProvider
        );
    }

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WhenSearchDisabled_ReturnsEarly()
    {
        // Arrange — disable search in the seeded config
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = false;
        await _fixture.DataContext.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — no search triggered, no arr client interaction
        _fixture.ArrClientFactory.Verify(
            x => x.GetClient(It.IsAny<InstanceType>(), It.IsAny<float>()),
            Times.Never);
        _fixture.EventPublisher.Verify(
            x => x.PublishSearchTriggered(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<SeekerSearchType>(),
                It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProactiveSearchDisabled_SkipsProactiveSearch()
    {
        // Arrange — search enabled but proactive disabled, no queue items
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = false;
        await _fixture.DataContext.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — no arr client interaction (no replacement items, proactive disabled)
        _fixture.ArrClientFactory.Verify(
            x => x.GetClient(It.IsAny<InstanceType>(), It.IsAny<float>()),
            Times.Never);
        _fixture.EventPublisher.Verify(
            x => x.PublishSearchTriggered(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<SeekerSearchType>(),
                It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReplacementItemExists_ProcessesReplacementFirst()
    {
        // Arrange
        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SearchQueue.Add(new SearchQueueItem
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            ItemId = 42,
            Title = "Test Movie",
            CreatedAt = DateTime.UtcNow
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = new Mock<IArrClient>();
        mockArrClient
            .Setup(x => x.SearchItemsAsync(It.IsAny<ArrInstance>(), It.IsAny<HashSet<SearchItem>>()))
            .ReturnsAsync([100L]);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered for the replacement item
        mockArrClient.Verify(
            x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()),
            Times.Once);

        _fixture.EventPublisher.Verify(
            x => x.PublishSearchTriggered(
                radarrInstance.Name,
                1,
                It.Is<IEnumerable<string>>(items => items.Contains("Test Movie")),
                SeekerSearchType.Replacement,
                It.IsAny<Guid?>()),
            Times.Once);

        // Replacement item should be removed from the queue
        var remaining = await _fixture.DataContext.SearchQueue.CountAsync();
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDryRunEnabled_DoesNotRemoveFromSearchQueue()
    {
        // Arrange
        _dryRunInterceptor.Setup(x => x.IsDryRunEnabled()).ReturnsAsync(true);

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SearchQueue.Add(new SearchQueueItem
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            ItemId = 42,
            Title = "Test Movie",
            CreatedAt = DateTime.UtcNow
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = new Mock<IArrClient>();
        mockArrClient
            .Setup(x => x.SearchItemsAsync(It.IsAny<ArrInstance>(), It.IsAny<HashSet<SearchItem>>()))
            .ReturnsAsync([100L]);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered but item stays in queue
        mockArrClient.Verify(
            x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()),
            Times.Once);

        var remaining = await _fixture.DataContext.SearchQueue.CountAsync();
        Assert.Equal(1, remaining);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActiveDownloadLimitReached_SkipsInstance()
    {
        // Arrange — enable proactive search with a Radarr instance
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        // Add a SeekerInstanceConfig with ActiveDownloadLimit = 2
        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            ActiveDownloadLimit = 2
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = new Mock<IArrClient>();
        // Current active downloads = 2, which meets the limit
        mockArrClient
            .Setup(x => x.GetActiveDownloadCountAsync(radarrInstance))
            .ReturnsAsync(2);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — no search triggered because active downloads >= limit
        mockArrClient.Verify(
            x => x.SearchItemsAsync(It.IsAny<ArrInstance>(), It.IsAny<HashSet<SearchItem>>()),
            Times.Never);

        _fixture.EventPublisher.Verify(
            x => x.PublishSearchTriggered(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<IEnumerable<string>>(),
                SeekerSearchType.Proactive,
                It.IsAny<Guid?>()),
            Times.Never);
    }

    #endregion
}

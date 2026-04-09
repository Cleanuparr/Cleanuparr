using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;
using Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;
using Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Http;
using Cleanuparr.Infrastructure.Hubs;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class DownloadServiceFactoryTests : IDisposable
{
    private readonly ILogger<DownloadServiceFactory> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly DownloadServiceFactory _factory;
    private readonly MemoryCache _memoryCache;

    public DownloadServiceFactoryTests()
    {
        _logger = Substitute.For<ILogger<DownloadServiceFactory>>();

        var services = new ServiceCollection();

        // Use real MemoryCache - mocks don't work properly with cache operations
        _memoryCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        services.AddSingleton<IMemoryCache>(_memoryCache);

        // Register loggers
        services.AddSingleton(Substitute.For<ILogger<QBitService>>());
        services.AddSingleton(Substitute.For<ILogger<DelugeService>>());
        services.AddSingleton(Substitute.For<ILogger<TransmissionService>>());
        services.AddSingleton(Substitute.For<ILogger<UTorrentService>>());

        services.AddSingleton(Substitute.For<IFilenameEvaluator>());
        services.AddSingleton(Substitute.For<IStriker>());
        services.AddSingleton(Substitute.For<IDryRunInterceptor>());
        services.AddSingleton(Substitute.For<IHardLinkFileService>());

        // IDynamicHttpClientProvider must return a real HttpClient for download services
        var httpClientProvider = Substitute.For<IDynamicHttpClientProvider>();
        httpClientProvider.CreateClient(Arg.Any<DownloadClientConfig>()).Returns(new HttpClient());
        services.AddSingleton(httpClientProvider);

        services.AddSingleton(Substitute.For<IQueueRuleEvaluator>());
        services.AddSingleton(Substitute.For<IQueueRuleManager>());
        services.AddSingleton(Substitute.For<ISeedingRuleEvaluator>());

        // UTorrentService needs ILoggerFactory
        services.AddLogging();

        // EventPublisher requires specific constructor arguments
        var eventsContextOptions = new DbContextOptionsBuilder<EventsContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var eventsContext = new EventsContext(eventsContextOptions);
        var hubContext = Substitute.For<IHubContext<AppHub>>();
        var clients = Substitute.For<IHubClients>();
        clients.All.Returns(Substitute.For<IClientProxy>());
        hubContext.Clients.Returns(clients);

        services.AddSingleton<IEventPublisher>(new EventPublisher(
            eventsContext,
            hubContext,
            Substitute.For<ILogger<EventPublisher>>(),
            Substitute.For<INotificationPublisher>(),
            Substitute.For<IDryRunInterceptor>()));

        // BlocklistProvider requires specific constructor arguments
        var scopeFactory = Substitute.For<IServiceScopeFactory>();

        services.AddSingleton<IBlocklistProvider>(new BlocklistProvider(
            Substitute.For<ILogger<BlocklistProvider>>(),
            scopeFactory,
            _memoryCache));

        _serviceProvider = services.BuildServiceProvider();
        _factory = new DownloadServiceFactory(_logger, _serviceProvider);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }

    #region GetDownloadService Tests

    [Fact]
    public void GetDownloadService_QBittorrent_ReturnsQBitService()
    {
        // Arrange
        var config = CreateClientConfig(DownloadClientTypeName.qBittorrent);

        // Act
        var service = _factory.GetDownloadService(config);

        // Assert
        Assert.NotNull(service);
        Assert.IsType<QBitService>(service);
    }

    [Fact]
    public void GetDownloadService_Deluge_ReturnsDelugeService()
    {
        // Arrange
        var config = CreateClientConfig(DownloadClientTypeName.Deluge);

        // Act
        var service = _factory.GetDownloadService(config);

        // Assert
        Assert.NotNull(service);
        Assert.IsType<DelugeService>(service);
    }

    [Fact]
    public void GetDownloadService_Transmission_ReturnsTransmissionService()
    {
        // Arrange
        var config = CreateClientConfig(DownloadClientTypeName.Transmission);

        // Act
        var service = _factory.GetDownloadService(config);

        // Assert
        Assert.NotNull(service);
        Assert.IsType<TransmissionService>(service);
    }

    [Fact]
    public void GetDownloadService_UTorrent_ReturnsUTorrentService()
    {
        // Arrange
        var config = CreateClientConfig(DownloadClientTypeName.uTorrent);

        // Act
        var service = _factory.GetDownloadService(config);

        // Assert
        Assert.NotNull(service);
        Assert.IsType<UTorrentService>(service);
    }

    [Fact]
    public void GetDownloadService_UnsupportedType_ThrowsNotSupportedException()
    {
        // Arrange
        var config = new DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = "Unsupported Client",
            TypeName = (DownloadClientTypeName)999, // Invalid type
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://test.example.com"),
            Enabled = true
        };

        // Act & Assert
        var exception = Assert.Throws<NotSupportedException>(() => _factory.GetDownloadService(config));
        Assert.Contains("not supported", exception.Message);
    }

    [Fact]
    public void GetDownloadService_DisabledClient_LogsWarningButReturnsService()
    {
        // Arrange
        var config = new DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = "Disabled qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://test.example.com"),
            Enabled = false
        };

        // Act
        var service = _factory.GetDownloadService(config);

        // Assert
        Assert.NotNull(service);
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void GetDownloadService_EnabledClient_DoesNotLogWarning()
    {
        // Arrange
        var config = CreateClientConfig(DownloadClientTypeName.qBittorrent);

        // Act
        var service = _factory.GetDownloadService(config);

        // Assert
        Assert.NotNull(service);
        _logger.DidNotReceive().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [InlineData(DownloadClientTypeName.qBittorrent, typeof(QBitService))]
    [InlineData(DownloadClientTypeName.Deluge, typeof(DelugeService))]
    [InlineData(DownloadClientTypeName.Transmission, typeof(TransmissionService))]
    [InlineData(DownloadClientTypeName.uTorrent, typeof(UTorrentService))]
    public void GetDownloadService_AllSupportedTypes_ReturnCorrectServiceType(
        DownloadClientTypeName typeName, Type expectedServiceType)
    {
        // Arrange
        var config = CreateClientConfig(typeName);

        // Act
        var service = _factory.GetDownloadService(config);

        // Assert
        Assert.NotNull(service);
        Assert.IsType(expectedServiceType, service);
    }

    [Fact]
    public void GetDownloadService_ReturnsNewInstanceEachTime()
    {
        // Arrange
        var config = CreateClientConfig(DownloadClientTypeName.qBittorrent);

        // Act
        var service1 = _factory.GetDownloadService(config);
        var service2 = _factory.GetDownloadService(config);

        // Assert
        Assert.NotSame(service1, service2);
    }

    #endregion

    #region Helper Methods

    private static DownloadClientConfig CreateClientConfig(DownloadClientTypeName typeName)
    {
        return new DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = $"Test {typeName} Client",
            TypeName = typeName,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://test.example.com"),
            Enabled = true
        };
    }

    #endregion
}

using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;
using Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;
using Cleanuparr.Infrastructure.Features.DownloadClient.RTorrent;
using Cleanuparr.Infrastructure.Features.DownloadClient.Sabnzbd;
using Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Http;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Realtime;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

/// <summary>
/// Guards the capability map in <see cref="DownloadClientTypeNameExtensions"/> against drifting from what
/// each client's service actually implements: a new client type, or a service that gains or drops an
/// <c>I*Capable</c> interface without updating the map, fails here instead of silently falling out of
/// whichever job narrows via <c>OfType&lt;T&gt;()</c>.
/// </summary>
public class DownloadClientCapabilityConsistencyTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DownloadServiceFactory _factory;
    private readonly MemoryCache _memoryCache;

    public DownloadClientCapabilityConsistencyTests()
    {
        var services = new ServiceCollection();

        _memoryCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        services.AddSingleton<IMemoryCache>(_memoryCache);

        services.AddSingleton(Substitute.For<ILogger<QBitService>>());
        services.AddSingleton(Substitute.For<ILogger<DelugeService>>());
        services.AddSingleton(Substitute.For<ILogger<TransmissionService>>());
        services.AddSingleton(Substitute.For<ILogger<UTorrentService>>());
        services.AddSingleton(Substitute.For<ILogger<RTorrentService>>());
        services.AddSingleton(Substitute.For<ILogger<SabnzbdService>>());
        services.AddSingleton(Substitute.For<ILogger<DownloadServiceFactory>>());

        services.AddSingleton(Substitute.For<IFilenameEvaluator>());
        services.AddSingleton(Substitute.For<IStriker>());
        services.AddSingleton(Substitute.For<IDryRunInterceptor>());
        services.AddSingleton(Substitute.For<IHardLinkFileService>());

        var httpClientProvider = Substitute.For<IDynamicHttpClientProvider>();
        httpClientProvider.CreateClient(Arg.Any<DownloadClientConfig>()).Returns(new HttpClient());
        services.AddSingleton(httpClientProvider);

        services.AddSingleton(Substitute.For<IQueueRuleEvaluator>());
        services.AddSingleton(Substitute.For<IQueueRuleManager>());
        services.AddSingleton(Substitute.For<ISeedingRuleEvaluator>());

        services.AddLogging();

        var eventsContextOptions = new DbContextOptionsBuilder<EventsContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var eventsContext = new EventsContext(eventsContextOptions);
        IEventNotifier eventNotifier = Substitute.For<IEventNotifier>();

        services.AddSingleton<IEventPublisher>(new EventPublisher(
            eventsContext,
            eventNotifier,
            Substitute.For<ILogger<EventPublisher>>(),
            Substitute.For<INotificationPublisher>(),
            Substitute.For<IDryRunInterceptor>(),
            new SqliteDatabaseProvider(),
            TimeProvider.System));

        var scopeFactory = Substitute.For<IServiceScopeFactory>();

        services.AddSingleton<IBlocklistProvider>(new BlocklistProvider(
            Substitute.For<ILogger<BlocklistProvider>>(),
            scopeFactory,
            _memoryCache,
            TimeProvider.System));

        services.AddSingleton(TimeProvider.System);

        _serviceProvider = services.BuildServiceProvider();
        _factory = new DownloadServiceFactory(Substitute.For<ILogger<DownloadServiceFactory>>(), _serviceProvider);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }

    public static IEnumerable<object[]> ClientTypeNames() =>
        EnumSentinel.SelectableValues<DownloadClientTypeName>().Select(typeName => new object[] { typeName });

    [Theory]
    [MemberData(nameof(ClientTypeNames))]
    public void SupportsCapability_MatchesServiceInterfaces(DownloadClientTypeName typeName)
    {
        DownloadClientConfig config = new()
        {
            Id = Guid.NewGuid(),
            Name = $"Test {typeName} client",
            TypeName = typeName,
            Type = typeName == DownloadClientTypeName.Sabnzbd ? DownloadClientType.Usenet : DownloadClientType.Torrent,
            Host = new Uri("http://test.example.com"),
            Enabled = true,
        };

        IDownloadService service = _factory.GetDownloadService(config);

        (service is IQueueCheckCapable).ShouldBe(
            typeName.SupportsCapability(DownloadClientCapability.QueueCheck),
            $"IQueueCheckCapable mismatch for {typeName}");

        (service is IFileBlockingCapable).ShouldBe(
            typeName.SupportsCapability(DownloadClientCapability.FileBlocking),
            $"IFileBlockingCapable mismatch for {typeName}");

        (service is IOrphanClaimsCapable).ShouldBe(
            typeName.SupportsCapability(DownloadClientCapability.OrphanClaims),
            $"IOrphanClaimsCapable mismatch for {typeName}");

        (service is ISeedingCleanupCapable).ShouldBe(
            typeName.SupportsCapability(DownloadClientCapability.SeedingCleanup),
            $"ISeedingCleanupCapable mismatch for {typeName}");

        (service is IUnlinkedCapable).ShouldBe(
            typeName.SupportsCapability(DownloadClientCapability.Unlinked),
            $"IUnlinkedCapable mismatch for {typeName}");

        (service is IDeadTorrentCapable).ShouldBe(
            typeName.SupportsCapability(DownloadClientCapability.DeadTorrent),
            $"IDeadTorrentCapable mismatch for {typeName}");
    }

    /// <summary>
    /// Maps a torrent client type to the <c>ISeedingRule</c> type it persists, so the capability map can be
    /// checked against what that rule type actually implements. <c>Sabnzbd</c> has no seeding rule type and
    /// is covered separately.
    /// </summary>
    private static readonly Dictionary<DownloadClientTypeName, Type> SeedingRuleTypes = new()
    {
        [DownloadClientTypeName.qBittorrent] = typeof(QBitSeedingRule),
        [DownloadClientTypeName.Deluge] = typeof(DelugeSeedingRule),
        [DownloadClientTypeName.Transmission] = typeof(TransmissionSeedingRule),
        [DownloadClientTypeName.uTorrent] = typeof(UTorrentSeedingRule),
        [DownloadClientTypeName.rTorrent] = typeof(RTorrentSeedingRule),
    };

    public static IEnumerable<object[]> TorrentClientTypeNames() =>
        SeedingRuleTypes.Keys.Select(typeName => new object[] { typeName });

    [Theory]
    [MemberData(nameof(TorrentClientTypeNames))]
    public void SupportsCapability_TagFiltering_MatchesSeedingRuleType(DownloadClientTypeName typeName)
    {
        bool ruleImplementsTagFilterable = SeedingRuleTypes[typeName].IsAssignableTo(typeof(ITagFilterable));

        typeName.SupportsCapability(DownloadClientCapability.TagFiltering).ShouldBe(
            ruleImplementsTagFilterable,
            $"TagFiltering mismatch for {typeName}");
    }

    [Theory]
    [MemberData(nameof(TorrentClientTypeNames))]
    public void SupportsCapability_SeedersFiltering_MatchesSeedingRuleType(DownloadClientTypeName typeName)
    {
        bool ruleImplementsSeedersFilterable = SeedingRuleTypes[typeName].IsAssignableTo(typeof(ISeedersFilterable));

        typeName.SupportsCapability(DownloadClientCapability.SeedersFiltering).ShouldBe(
            ruleImplementsSeedersFilterable,
            $"SeedersFiltering mismatch for {typeName}");
    }

    [Fact]
    public void EveryClientType_HasAMapEntry()
    {
        // SupportsCapability returns false, never throws, for a type name missing from the map, so a type with
        // no entry at all reads as "supports nothing" rather than failing loudly. Every real client supports
        // at least one capability, so a type matching none of them has fallen out of the map.
        foreach (DownloadClientTypeName typeName in EnumSentinel.SelectableValues<DownloadClientTypeName>())
        {
            bool supportsAny = Enum.GetValues<DownloadClientCapability>().Any(capability => typeName.SupportsCapability(capability));

            supportsAny.ShouldBeTrue($"{typeName} is missing from the DownloadClientTypeNameExtensions capability map");
        }
    }
}

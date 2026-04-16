using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Cleanuparr.Infrastructure.Http;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Services;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.Logging;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using NSubstitute;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class QBitServiceFixture : IDisposable
{
    public ILogger<QBitService> Logger { get; private set; }
    public IFilenameEvaluator FilenameEvaluator { get; private set; }
    public IStriker Striker { get; private set; }
    public IDryRunInterceptor DryRunInterceptor { get; private set; }
    public IHardLinkFileService HardLinkFileService { get; private set; }
    public IDynamicHttpClientProvider HttpClientProvider { get; private set; }
    public IEventPublisher EventPublisher { get; private set; }
    public IBlocklistProvider BlocklistProvider { get; private set; }
    public IQueueRuleEvaluator RuleEvaluator { get; private set; }
    public IQueueRuleManager RuleManager { get; private set; }
    public ISeedingRuleEvaluator SeedingRuleEvaluator { get; private set; }
    public IQBittorrentClientWrapper ClientWrapper { get; private set; }

    public QBitServiceFixture()
    {
        SubstituteHelper.ClearPendingArgSpecs();
        Logger = Substitute.For<ILogger<QBitService>>();
        FilenameEvaluator = Substitute.For<IFilenameEvaluator>();
        Striker = Substitute.For<IStriker>();
        DryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        HardLinkFileService = Substitute.For<IHardLinkFileService>();
        HttpClientProvider = Substitute.For<IDynamicHttpClientProvider>();
        EventPublisher = Substitute.For<IEventPublisher>();
        BlocklistProvider = Substitute.For<IBlocklistProvider>();
        RuleEvaluator = Substitute.For<IQueueRuleEvaluator>();
        RuleManager = Substitute.For<IQueueRuleManager>();
        SeedingRuleEvaluator = Substitute.For<ISeedingRuleEvaluator>();
        ClientWrapper = Substitute.For<IQBittorrentClientWrapper>();

        // Setup default behavior for DryRunInterceptor to execute actions directly
        DryRunInterceptor
            .InterceptAsync(default!, default!)
            .ReturnsForAnyArgs(callInfo =>
            {
                var action = callInfo.ArgAt<Delegate>(0);
                var parameters = callInfo.ArgAt<object[]>(1);
                return (Task)(action.DynamicInvoke(parameters) ?? Task.CompletedTask);
            });

        SetupSeedingRuleEvaluator();
    }

    public QBitService CreateSut(DownloadClientConfig? config = null)
    {
        config ??= new DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = "Test Client",
            TypeName = Domain.Enums.DownloadClientTypeName.qBittorrent,
            Type = Domain.Enums.DownloadClientType.Torrent,
            Enabled = true,
            Host = new Uri("http://localhost:8080"),
            Username = "admin",
            Password = "admin",
            UrlBase = ""
        };

        // Setup HTTP client provider
        var httpClient = new HttpClient();
        HttpClientProvider
            .CreateClient(Arg.Any<DownloadClientConfig>())
            .Returns(httpClient);

        return new QBitService(
            Logger,
            FilenameEvaluator,
            Striker,
            DryRunInterceptor,
            HardLinkFileService,
            HttpClientProvider,
            EventPublisher,
            BlocklistProvider,
            config,
            RuleEvaluator,
            SeedingRuleEvaluator,
            ClientWrapper
        );
    }

    public void ResetMocks()
    {
        SubstituteHelper.ClearPendingArgSpecs();
        Logger = Substitute.For<ILogger<QBitService>>();
        FilenameEvaluator = Substitute.For<IFilenameEvaluator>();
        Striker = Substitute.For<IStriker>();
        DryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        HardLinkFileService = Substitute.For<IHardLinkFileService>();
        HttpClientProvider = Substitute.For<IDynamicHttpClientProvider>();
        EventPublisher = Substitute.For<IEventPublisher>();
        BlocklistProvider = Substitute.For<IBlocklistProvider>();
        RuleEvaluator = Substitute.For<IQueueRuleEvaluator>();
        RuleManager = Substitute.For<IQueueRuleManager>();
        SeedingRuleEvaluator = Substitute.For<ISeedingRuleEvaluator>();
        ClientWrapper = Substitute.For<IQBittorrentClientWrapper>();

        // Re-setup default DryRunInterceptor behavior
        DryRunInterceptor
            .InterceptAsync(default!, default!)
            .ReturnsForAnyArgs(callInfo =>
            {
                var action = callInfo.ArgAt<Delegate>(0);
                var parameters = callInfo.ArgAt<object[]>(1);
                return (Task)(action.DynamicInvoke(parameters) ?? Task.CompletedTask);
            });

        SetupSeedingRuleEvaluator();
    }

    private void SetupSeedingRuleEvaluator()
    {
        var realEvaluator = new SeedingRuleEvaluator();
        SeedingRuleEvaluator
            .GetMatchingRule(default!, default!)
            .ReturnsForAnyArgs(callInfo =>
                realEvaluator.GetMatchingRule(callInfo.Arg<Domain.Entities.ITorrentItemWrapper>(), callInfo.Arg<IEnumerable<ISeedingRule>>()));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}

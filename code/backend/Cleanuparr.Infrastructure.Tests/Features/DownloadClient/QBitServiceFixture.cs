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
using Moq;
using NSubstitute;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class QBitServiceFixture : IDisposable
{
    public Mock<ILogger<QBitService>> Logger { get; }
    public Mock<IFilenameEvaluator> FilenameEvaluator { get; }
    public Mock<IStriker> Striker { get; }
    public Mock<IDryRunInterceptor> DryRunInterceptor { get; }
    public Mock<IHardLinkFileService> HardLinkFileService { get; }
    public Mock<IDynamicHttpClientProvider> HttpClientProvider { get; }
    public Mock<IEventPublisher> EventPublisher { get; }
    public Mock<IBlocklistProvider> BlocklistProvider { get; }
    public Mock<IQueueRuleEvaluator> RuleEvaluator { get; }
    public Mock<IQueueRuleManager> RuleManager { get; }
    public ISeedingRuleEvaluator SeedingRuleEvaluator { get; private set; }
    public Mock<IQBittorrentClientWrapper> ClientWrapper { get; }

    public QBitServiceFixture()
    {
        Logger = new Mock<ILogger<QBitService>>();
        FilenameEvaluator = new Mock<IFilenameEvaluator>();
        Striker = new Mock<IStriker>();
        DryRunInterceptor = new Mock<IDryRunInterceptor>();
        HardLinkFileService = new Mock<IHardLinkFileService>();
        HttpClientProvider = new Mock<IDynamicHttpClientProvider>();
        EventPublisher = new Mock<IEventPublisher>();
        BlocklistProvider =new Mock<IBlocklistProvider>();
        RuleEvaluator = new Mock<IQueueRuleEvaluator>();
        RuleManager = new Mock<IQueueRuleManager>();
        SeedingRuleEvaluator = Substitute.For<ISeedingRuleEvaluator>();
        ClientWrapper = new Mock<IQBittorrentClientWrapper>();

        // Setup default behavior for DryRunInterceptor to execute actions directly
        DryRunInterceptor
            .Setup(x => x.InterceptAsync(It.IsAny<Delegate>(), It.IsAny<object[]>()))
            .Returns((Delegate action, object[] parameters) =>
            {
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
            .Setup(x => x.CreateClient(It.IsAny<DownloadClientConfig>()))
            .Returns(httpClient);

        return new QBitService(
            Logger.Object,
            FilenameEvaluator.Object,
            Striker.Object,
            DryRunInterceptor.Object,
            HardLinkFileService.Object,
            HttpClientProvider.Object,
            EventPublisher.Object,
            BlocklistProvider.Object,
            config,
            RuleEvaluator.Object,
            SeedingRuleEvaluator,
            ClientWrapper.Object
        );
    }

    public void ResetMocks()
    {
        Logger.Reset();
        FilenameEvaluator.Reset();
        Striker.Reset();
        DryRunInterceptor.Reset();
        HardLinkFileService.Reset();
        HttpClientProvider.Reset();
        EventPublisher.Reset();
        RuleEvaluator.Reset();
        RuleManager.Reset();
        SeedingRuleEvaluator = Substitute.For<ISeedingRuleEvaluator>();
        ClientWrapper.Reset();

        // Re-setup default DryRunInterceptor behavior
        DryRunInterceptor
            .Setup(x => x.InterceptAsync(It.IsAny<Delegate>(), It.IsAny<object[]>()))
            .Returns((Delegate action, object[] parameters) =>
            {
                return (Task)(action.DynamicInvoke(parameters) ?? Task.CompletedTask);
            });

        SetupSeedingRuleEvaluator();
    }

    private void SetupSeedingRuleEvaluator()
    {
        var realEvaluator = new SeedingRuleEvaluator();
        SeedingRuleEvaluator
            .GetMatchingRule(Arg.Any<Domain.Entities.ITorrentItemWrapper>(), Arg.Any<IEnumerable<ISeedingRule>>())
            .Returns(callInfo =>
                realEvaluator.GetMatchingRule(callInfo.Arg<Domain.Entities.ITorrentItemWrapper>(), callInfo.Arg<IEnumerable<ISeedingRule>>()));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}

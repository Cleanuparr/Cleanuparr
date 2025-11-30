using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Cleanuparr.Infrastructure.Http;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Infrastructure.Tests.Features.DownloadClient.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class DelugeServiceFixture : IDisposable
{
    public Mock<ILogger<DelugeService>> Logger { get; }
    public MemoryCache Cache { get; }
    public Mock<IFilenameEvaluator> FilenameEvaluator { get; }
    public Mock<IStriker> Striker { get; }
    public Mock<IDryRunInterceptor> DryRunInterceptor { get; }
    public Mock<IHardLinkFileService> HardLinkFileService { get; }
    public Mock<IDynamicHttpClientProvider> HttpClientProvider { get; }
    public EventPublisher EventPublisher { get; }
    public BlocklistProvider BlocklistProvider { get; }
    public Mock<IRuleEvaluator> RuleEvaluator { get; }
    public Mock<IRuleManager> RuleManager { get; }
    public Mock<IDelugeClientWrapper> ClientWrapper { get; }

    public DelugeServiceFixture()
    {
        Logger = new Mock<ILogger<DelugeService>>();
        Cache = new MemoryCache(new MemoryCacheOptions());
        FilenameEvaluator = new Mock<IFilenameEvaluator>();
        Striker = new Mock<IStriker>();
        DryRunInterceptor = new Mock<IDryRunInterceptor>();
        HardLinkFileService = new Mock<IHardLinkFileService>();
        HttpClientProvider = new Mock<IDynamicHttpClientProvider>();
        EventPublisher = TestEventPublisherFactory.Create();
        BlocklistProvider = TestBlocklistProviderFactory.Create();
        RuleEvaluator = new Mock<IRuleEvaluator>();
        RuleManager = new Mock<IRuleManager>();
        ClientWrapper = new Mock<IDelugeClientWrapper>();

        DryRunInterceptor
            .Setup(x => x.InterceptAsync(It.IsAny<Delegate>(), It.IsAny<object[]>()))
            .Returns((Delegate action, object[] parameters) =>
            {
                return (Task)(action.DynamicInvoke(parameters) ?? Task.CompletedTask);
            });
    }

    public DelugeService CreateSut(DownloadClientConfig? config = null)
    {
        config ??= new DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = "Test Client",
            TypeName = Domain.Enums.DownloadClientTypeName.Deluge,
            Type = Domain.Enums.DownloadClientType.Torrent,
            Enabled = true,
            Host = new Uri("http://localhost:8112"),
            Username = "admin",
            Password = "admin",
            UrlBase = ""
        };

        var httpClient = new HttpClient();
        HttpClientProvider
            .Setup(x => x.CreateClient(It.IsAny<DownloadClientConfig>()))
            .Returns(httpClient);

        return new DelugeService(
            Logger.Object,
            Cache,
            FilenameEvaluator.Object,
            Striker.Object,
            DryRunInterceptor.Object,
            HardLinkFileService.Object,
            HttpClientProvider.Object,
            EventPublisher,
            BlocklistProvider,
            config,
            RuleEvaluator.Object,
            RuleManager.Object,
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
        RuleEvaluator.Reset();
        RuleManager.Reset();
        ClientWrapper.Reset();

        DryRunInterceptor
            .Setup(x => x.InterceptAsync(It.IsAny<Delegate>(), It.IsAny<object[]>()))
            .Returns((Delegate action, object[] parameters) =>
            {
                return (Task)(action.DynamicInvoke(parameters) ?? Task.CompletedTask);
            });
    }

    public void Dispose()
    {
        Cache.Dispose();
        GC.SuppressFinalize(this);
    }
}

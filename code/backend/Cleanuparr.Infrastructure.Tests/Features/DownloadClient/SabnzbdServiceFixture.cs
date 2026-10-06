using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadClient.Sabnzbd;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Cleanuparr.Infrastructure.Http;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.Logging;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using NSubstitute;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class SabnzbdServiceFixture : IDisposable
{
    public ILogger<SabnzbdService> Logger { get; private set; }
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
    public ISabnzbdClientWrapper ClientWrapper { get; private set; }

    public SabnzbdServiceFixture()
    {
        SubstituteHelper.ClearPendingArgSpecs();
        Logger = Substitute.For<ILogger<SabnzbdService>>();
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
        ClientWrapper = Substitute.For<ISabnzbdClientWrapper>();

        DryRunInterceptor
            .InterceptAsync(Arg.Any<Func<Task>>(), Arg.Any<string?>())
            .ReturnsForAnyArgs(callInfo => callInfo.ArgAt<Func<Task>>(0).Invoke());
    }

    public SabnzbdService CreateSut(DownloadClientConfig? config = null)
    {
        config ??= new DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = "Test Client",
            TypeName = Domain.Enums.DownloadClientTypeName.Sabnzbd,
            Type = Domain.Enums.DownloadClientType.Usenet,
            Enabled = true,
            Host = new Uri("http://localhost:8080"),
            ApiKey = "test-api-key",
            UrlBase = ""
        };

        var httpClient = new HttpClient();
        HttpClientProvider
            .CreateClient(Arg.Any<DownloadClientConfig>())
            .Returns(httpClient);

        return new SabnzbdService(
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
            TimeProvider.System,
            ClientWrapper
        );
    }

    public void ResetMocks()
    {
        SubstituteHelper.ClearPendingArgSpecs();
        Logger = Substitute.For<ILogger<SabnzbdService>>();
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
        ClientWrapper = Substitute.For<ISabnzbdClientWrapper>();

        DryRunInterceptor
            .InterceptAsync(Arg.Any<Func<Task>>(), Arg.Any<string?>())
            .ReturnsForAnyArgs(callInfo => callInfo.ArgAt<Func<Task>>(0).Invoke());
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}

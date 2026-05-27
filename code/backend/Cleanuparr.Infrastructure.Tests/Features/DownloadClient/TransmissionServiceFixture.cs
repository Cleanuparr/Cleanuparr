using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;
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

public class TransmissionServiceFixture : IDisposable
{
    public ILogger<TransmissionService> Logger { get; private set; }
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
    public ITransmissionClientWrapper ClientWrapper { get; private set; }

    public TransmissionServiceFixture()
    {
        SubstituteHelper.ClearPendingArgSpecs();
        Logger = Substitute.For<ILogger<TransmissionService>>();
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
        ClientWrapper = Substitute.For<ITransmissionClientWrapper>();

        DryRunInterceptor
            .InterceptAsync(default!, default!)
            .ReturnsForAnyArgs(callInfo =>
            {
                var action = callInfo.ArgAt<Delegate>(0);
                var parameters = callInfo.ArgAt<object[]>(1);
                return (Task)(action.DynamicInvoke(parameters) ?? Task.CompletedTask);
            });
    }

    public TransmissionService CreateSut(DownloadClientConfig? config = null)
    {
        config ??= new DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = "Test Client",
            TypeName = Domain.Enums.DownloadClientTypeName.Transmission,
            Type = Domain.Enums.DownloadClientType.Torrent,
            Enabled = true,
            Host = new Uri("http://localhost:9091"),
            Username = "admin",
            Password = "admin",
            UrlBase = "/transmission"
        };

        var httpClient = new HttpClient();
        HttpClientProvider
            .CreateClient(Arg.Any<DownloadClientConfig>())
            .Returns(httpClient);

        return new TransmissionService(
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
        Logger = Substitute.For<ILogger<TransmissionService>>();
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
        ClientWrapper = Substitute.For<ITransmissionClientWrapper>();

        DryRunInterceptor
            .InterceptAsync(default!, default!)
            .ReturnsForAnyArgs(callInfo =>
            {
                var action = callInfo.ArgAt<Delegate>(0);
                var parameters = callInfo.ArgAt<object[]>(1);
                return (Task)(action.DynamicInvoke(parameters) ?? Task.CompletedTask);
            });
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}

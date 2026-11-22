using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Cleanuparr.Infrastructure.Http;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

/// <summary>
/// Uses the public constructor, since the test constructor skips the FLM.Transmission client.
/// </summary>
public sealed class TransmissionServiceHttpAuthTests
{
    private static (TransmissionService service, FakeHttpMessageHandler handler) CreateService(HttpAuthType httpAuthType)
    {
        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("""{"result":"success","arguments":{}}""")
        }));

        DownloadClientConfig config = new()
        {
            Id = Guid.NewGuid(),
            Name = "transmission",
            TypeName = DownloadClientTypeName.Transmission,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:9091"),
            HttpAuthType = httpAuthType,
            Username = "user",
            Password = "pass",
        };

        IDynamicHttpClientProvider httpClientProvider = Substitute.For<IDynamicHttpClientProvider>();
        httpClientProvider.CreateClient(Arg.Any<DownloadClientConfig>()).Returns(new HttpClient(handler));

        IDryRunInterceptor dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        dryRunInterceptor
            .InterceptAsync(Arg.Any<Func<Task>>(), Arg.Any<string?>())
            .ReturnsForAnyArgs(callInfo => callInfo.ArgAt<Func<Task>>(0).Invoke());

        TransmissionService service = new(
            NullLogger<TransmissionService>.Instance,
            Substitute.For<IFilenameEvaluator>(),
            Substitute.For<IStriker>(),
            dryRunInterceptor,
            Substitute.For<IHardLinkFileService>(),
            httpClientProvider,
            Substitute.For<IEventPublisher>(),
            Substitute.For<IBlocklistProvider>(),
            config,
            Substitute.For<IQueueRuleEvaluator>(),
            Substitute.For<ISeedingRuleEvaluator>(),
            TimeProvider.System);

        return (service, handler);
    }

    [Fact]
    public async Task LoginAsync_InBasicMode_SendsBasicHeader()
    {
        (TransmissionService service, FakeHttpMessageHandler handler) = CreateService(HttpAuthType.Basic);

        await service.LoginAsync();

        handler.CapturedRequests[0].Headers.Authorization!.Scheme.ShouldBe("Basic");
    }

    [Fact]
    public async Task LoginAsync_InDigestMode_SendsNoManualHeader()
    {
        (TransmissionService service, FakeHttpMessageHandler handler) = CreateService(HttpAuthType.Digest);

        await service.LoginAsync();

        handler.CapturedRequests[0].Headers.Authorization.ShouldBeNull();
    }
}

using Cleanuparr.Application.Features.ContentBlocker;
using Cleanuparr.Application.Features.DownloadCleaner;
using Cleanuparr.Application.Features.QueueCleaner;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.ContentBlocker;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadHunter;
using Cleanuparr.Infrastructure.Features.DownloadHunter.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadRemover;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Interfaces;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.Security;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Services;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Infrastructure.Interceptors;
using Infrastructure.Verticals.Files;

namespace Cleanuparr.Api.DependencyInjection;

public static class ServicesDI
{
    public static IServiceCollection AddServices(this IServiceCollection services) =>
        services
            .AddSingleton<IEncryptionService, AesEncryptionService>()
            .AddTransient<SensitiveDataJsonConverter>()
            .AddTransient<EventsContext>()
            .AddTransient<DataContext>()
            .AddTransient<EventPublisher>()
            .AddHostedService<EventCleanupService>()
            // API services
            .AddSingleton<IJobManagementService, JobManagementService>()
            // Core services
            .AddTransient<IDryRunInterceptor, DryRunInterceptor>()
            .AddTransient<CertificateValidationService>()
            .AddTransient<SonarrClient>()
            .AddTransient<RadarrClient>()
            .AddTransient<LidarrClient>()
            .AddTransient<ReadarrClient>()
            .AddTransient<WhisparrClient>()
            .AddTransient<ArrClientFactory>()
            .AddTransient<QueueCleaner>()
            .AddTransient<ContentBlocker>()
            .AddTransient<DownloadCleaner>()
            .AddTransient<IQueueItemRemover, QueueItemRemover>()
            .AddTransient<IDownloadHunter, DownloadHunter>()
            .AddTransient<IFilenameEvaluator, FilenameEvaluator>()
            .AddTransient<IHardLinkFileService, HardLinkFileService>()
            .AddTransient<UnixHardLinkFileService>()
            .AddTransient<WindowsHardLinkFileService>()
            .AddTransient<ArrQueueIterator>()
            .AddTransient<DownloadServiceFactory>()
            .AddTransient<IStriker, Striker>()
            .AddSingleton<BlocklistProvider>();
}
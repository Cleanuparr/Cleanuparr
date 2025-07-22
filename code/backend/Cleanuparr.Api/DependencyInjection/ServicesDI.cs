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
            .AddScoped<IEncryptionService, AesEncryptionService>()
            .AddScoped<SensitiveDataJsonConverter>()
            .AddScoped<EventsContext>()
            .AddScoped<DataContext>()
            .AddScoped<EventPublisher>()
            .AddHostedService<EventCleanupService>()
            .AddScoped<IDryRunInterceptor, DryRunInterceptor>()
            .AddScoped<CertificateValidationService>()
            .AddScoped<SonarrClient>()
            .AddScoped<RadarrClient>()
            .AddScoped<LidarrClient>()
            .AddScoped<ReadarrClient>()
            .AddScoped<WhisparrClient>()
            .AddScoped<ArrClientFactory>()
            .AddScoped<QueueCleaner>()
            .AddScoped<ContentBlocker>()
            .AddScoped<DownloadCleaner>()
            .AddScoped<IQueueItemRemover, QueueItemRemover>()
            .AddScoped<IDownloadHunter, DownloadHunter>()
            .AddScoped<IFilenameEvaluator, FilenameEvaluator>()
            .AddScoped<IHardLinkFileService, HardLinkFileService>()
            .AddScoped<UnixHardLinkFileService>()
            .AddScoped<WindowsHardLinkFileService>()
            .AddScoped<ArrQueueIterator>()
            .AddScoped<DownloadServiceFactory>()
            .AddScoped<IStriker, Striker>()
            .AddSingleton<IJobManagementService, JobManagementService>()
            .AddSingleton<BlocklistProvider>();
}
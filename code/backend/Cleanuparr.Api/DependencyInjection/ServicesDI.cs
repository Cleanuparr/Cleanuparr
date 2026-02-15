using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Auth;
using Cleanuparr.Infrastructure.Features.BlacklistSync;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadHunter;
using Cleanuparr.Infrastructure.Features.DownloadHunter.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadRemover;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Interfaces;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Services;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;

namespace Cleanuparr.Api.DependencyInjection;

public static class ServicesDI
{
    public static IServiceCollection AddServices(this IServiceCollection services) =>
        services
            .AddScoped<EventsContext>()
            .AddScoped<DataContext>()
            .AddScoped<UsersContext>()
            .AddSingleton<IJwtService, JwtService>()
            .AddSingleton<IPasswordService, PasswordService>()
            .AddSingleton<ITotpService, TotpService>()
            .AddScoped<IPlexAuthService, PlexAuthService>()
            .AddScoped<IEventPublisher, EventPublisher>()
            .AddHostedService<EventCleanupService>()
            .AddScoped<IDryRunInterceptor, DryRunInterceptor>()
            .AddScoped<CertificateValidationService>()
            .AddScoped<ISonarrClient, SonarrClient>()
            .AddScoped<IRadarrClient, RadarrClient>()
            .AddScoped<ILidarrClient, LidarrClient>()
            .AddScoped<IReadarrClient, ReadarrClient>()
            .AddScoped<IWhisparrV2Client, WhisparrV2Client>()
            .AddScoped<IWhisparrV3Client, WhisparrV3Client>()
            .AddScoped<IArrClientFactory, ArrClientFactory>()
            .AddScoped<QueueCleaner>()
            .AddScoped<BlacklistSynchronizer>()
            .AddScoped<MalwareBlocker>()
            .AddScoped<DownloadCleaner>()
            .AddScoped<IQueueItemRemover, QueueItemRemover>()
            .AddScoped<IDownloadHunter, DownloadHunter>()
            .AddScoped<IFilenameEvaluator, FilenameEvaluator>()
            .AddScoped<IHardLinkFileService, HardLinkFileService>()
            .AddScoped<IUnixHardLinkFileService, UnixHardLinkFileService>()
            .AddScoped<IWindowsHardLinkFileService, WindowsHardLinkFileService>()
            .AddScoped<IArrQueueIterator, ArrQueueIterator>()
            .AddScoped<IDownloadServiceFactory, DownloadServiceFactory>()
            .AddScoped<IStriker, Striker>()
            .AddScoped<FileReader>()
            .AddScoped<IRuleManager, RuleManager>()
            .AddScoped<IRuleEvaluator, RuleEvaluator>()
            .AddScoped<IRuleIntervalValidator, RuleIntervalValidator>()
            .AddSingleton<IJobManagementService, JobManagementService>()
            .AddSingleton<IBlocklistProvider, BlocklistProvider>()
            .AddSingleton(TimeProvider.System)
            .AddSingleton<AppStatusSnapshot>()
            .AddHostedService<AppStatusRefreshService>();
}
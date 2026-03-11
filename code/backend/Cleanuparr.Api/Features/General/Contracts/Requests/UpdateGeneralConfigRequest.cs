using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Cleanuparr.Infrastructure.Logging;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Shared.Helpers;
using Serilog.Events;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Api.Features.General.Contracts.Requests;

public sealed record UpdateGeneralConfigRequest
{
    public bool DisplaySupportBanner { get; init; } = true;

    public bool DryRun { get; init; }

    public ushort HttpMaxRetries { get; init; }

    public ushort HttpTimeout { get; init; } = 100;

    public CertificateValidationType HttpCertificateValidation { get; init; } = CertificateValidationType.Enabled;

    public bool SearchEnabled { get; init; } = true;

    public ushort SearchDelay { get; init; } = Constants.DefaultSearchDelaySeconds;

    public bool StatusCheckEnabled { get; init; } = true;

    public string EncryptionKey { get; init; } = Guid.NewGuid().ToString();

    public List<string> IgnoredDownloads { get; init; } = [];

    public ushort StrikeInactivityWindowHours { get; init; } = 24;

    public UpdateLoggingConfigRequest Log { get; init; } = new();

    public UpdateAuthConfigRequest Auth { get; init; } = new();

    public GeneralConfig ApplyTo(GeneralConfig existingConfig, IServiceProvider services, ILogger logger)
    {
        existingConfig.DisplaySupportBanner = DisplaySupportBanner;
        existingConfig.DryRun = DryRun;
        existingConfig.HttpMaxRetries = HttpMaxRetries;
        existingConfig.HttpTimeout = HttpTimeout;
        existingConfig.HttpCertificateValidation = HttpCertificateValidation;
        existingConfig.SearchEnabled = SearchEnabled;
        existingConfig.SearchDelay = SearchDelay;
        existingConfig.StatusCheckEnabled = StatusCheckEnabled;
        existingConfig.EncryptionKey = EncryptionKey;
        existingConfig.IgnoredDownloads = IgnoredDownloads;
        existingConfig.StrikeInactivityWindowHours = StrikeInactivityWindowHours;

        bool loggingChanged = Log.ApplyTo(existingConfig.Log);
        Auth.ApplyTo(existingConfig.Auth);

        Validate(existingConfig);

        ApplySideEffects(existingConfig, services, logger, loggingChanged);

        return existingConfig;
    }

    private static void Validate(GeneralConfig config)
    {
        if (config.HttpTimeout is 0)
        {
            throw new ValidationException("HTTP_TIMEOUT must be greater than 0");
        }

        if (config.StrikeInactivityWindowHours is 0)
        {
            throw new ValidationException("STRIKE_INACTIVITY_WINDOW_HOURS must be greater than 0");
        }

        if (config.StrikeInactivityWindowHours > 168)
        {
            throw new ValidationException("STRIKE_INACTIVITY_WINDOW_HOURS must be less than or equal to 168");
        }

        config.Log.Validate();
        config.Auth.Validate();
    }

    private void ApplySideEffects(GeneralConfig config, IServiceProvider services, ILogger logger, bool loggingChanged)
    {
        var dynamicHttpClientFactory = services.GetRequiredService<IDynamicHttpClientFactory>();
        dynamicHttpClientFactory.UpdateAllClientsFromGeneralConfig(config);

        logger.LogInformation("Updated all HTTP client configurations with new general settings");

        if (!loggingChanged)
        {
            return;
        }

        if (Log.LevelOnlyChange)
        {
            logger.LogCritical("Setting global log level to {level}", config.Log.Level);
            LoggingConfigManager.SetLogLevel(config.Log.Level);
            return;
        }

        logger.LogCritical("Reconfiguring logger due to configuration changes");
        LoggingConfigManager.ReconfigureLogging(config);
    }
}

public sealed record UpdateLoggingConfigRequest
{
    public LogEventLevel Level { get; init; } = LogEventLevel.Information;

    public ushort RollingSizeMB { get; init; } = 10;

    public ushort RetainedFileCount { get; init; } = 5;

    public ushort TimeLimitHours { get; init; } = 24;

    public bool ArchiveEnabled { get; init; } = true;

    public ushort ArchiveRetainedCount { get; init; } = 60;

    public ushort ArchiveTimeLimitHours { get; init; } = 24 * 30;

    public bool ApplyTo(LoggingConfig existingConfig)
    {
        bool levelChanged = existingConfig.Level != Level;
        bool otherPropertiesChanged =
            existingConfig.RollingSizeMB != RollingSizeMB ||
            existingConfig.RetainedFileCount != RetainedFileCount ||
            existingConfig.TimeLimitHours != TimeLimitHours ||
            existingConfig.ArchiveEnabled != ArchiveEnabled ||
            existingConfig.ArchiveRetainedCount != ArchiveRetainedCount ||
            existingConfig.ArchiveTimeLimitHours != ArchiveTimeLimitHours;

        existingConfig.Level = Level;
        existingConfig.RollingSizeMB = RollingSizeMB;
        existingConfig.RetainedFileCount = RetainedFileCount;
        existingConfig.TimeLimitHours = TimeLimitHours;
        existingConfig.ArchiveEnabled = ArchiveEnabled;
        existingConfig.ArchiveRetainedCount = ArchiveRetainedCount;
        existingConfig.ArchiveTimeLimitHours = ArchiveTimeLimitHours;

        existingConfig.Validate();

        LevelOnlyChange = levelChanged && !otherPropertiesChanged;

        return levelChanged || otherPropertiesChanged;
    }

    public bool LevelOnlyChange { get; private set; }
}

public sealed record UpdateAuthConfigRequest
{
    public bool DisableAuthForLocalAddresses { get; init; }

    public bool TrustForwardedHeaders { get; init; }

    public List<string> TrustedNetworks { get; init; } = [];

    public void ApplyTo(AuthConfig existingConfig)
    {
        existingConfig.DisableAuthForLocalAddresses = DisableAuthForLocalAddresses;
        existingConfig.TrustForwardedHeaders = TrustForwardedHeaders;
        existingConfig.TrustedNetworks = TrustedNetworks;
    }
}

using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.General;

namespace Cleanuparr.Api.Features.General.Contracts.Responses;

public sealed record GeneralConfigResponse
{
    public bool DisplaySupportBanner { get; init; }

    public bool DryRun { get; init; }

    public ushort HttpMaxRetries { get; init; }

    public ushort HttpTimeout { get; init; }

    public CertificateValidationType HttpCertificateValidation { get; init; }

    public bool HttpSendUserAgent { get; init; }

    public bool StatusCheckEnabled { get; init; }

    public required IReadOnlyList<string> IgnoredDownloads { get; init; }

    public bool ConnectivityCheckEnabled { get; init; }

    public required IReadOnlyList<string> ConnectivityCheckUrls { get; init; }

    public ushort StrikeInactivityWindowHours { get; init; }

    public ushort HistoryRetentionDays { get; init; }

    public required LoggingConfigResponse Log { get; init; }

    public required AuthConfigResponse Auth { get; init; }

    public static GeneralConfigResponse From(GeneralConfig config) => new()
    {
        DisplaySupportBanner = config.DisplaySupportBanner,
        DryRun = config.DryRun,
        HttpMaxRetries = config.HttpMaxRetries,
        HttpTimeout = config.HttpTimeout,
        HttpCertificateValidation = config.HttpCertificateValidation,
        HttpSendUserAgent = config.HttpSendUserAgent,
        StatusCheckEnabled = config.StatusCheckEnabled,
        IgnoredDownloads = config.IgnoredDownloads,
        ConnectivityCheckEnabled = config.ConnectivityCheckEnabled,
        ConnectivityCheckUrls = config.ConnectivityCheckUrls,
        StrikeInactivityWindowHours = config.StrikeInactivityWindowHours,
        HistoryRetentionDays = config.HistoryRetentionDays,
        Log = LoggingConfigResponse.From(config.Log),
        Auth = AuthConfigResponse.From(config.Auth),
    };
}

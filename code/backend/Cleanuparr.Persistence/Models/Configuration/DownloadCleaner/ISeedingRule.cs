using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

public interface ISeedingRule : IConfig
{
    Guid Id { get; }

    Guid DownloadClientConfigId { get; set; }

    DownloadClientConfig DownloadClientConfig { get; set; }

    string Name { get; }

    TorrentPrivacyType PrivacyType { get; }

    double MaxRatio { get; }

    double MinSeedTime { get; }

    double MaxSeedTime { get; }

    bool DeleteSourceFiles { get; }
}

using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

public interface ISeedingRule : IConfig
{
    Guid Id { get; set; }

    Guid DownloadClientConfigId { get; set; }

    DownloadClientConfig DownloadClientConfig { get; set; }

    string Name { get; set; }

    TorrentPrivacyType PrivacyType { get; set; }

    double MaxRatio { get; set; }

    double MinSeedTime { get; set; }

    double MaxSeedTime { get; set; }

    bool DeleteSourceFiles { get; set; }
}

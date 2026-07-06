namespace Cleanuparr.Infrastructure.Stats;

public class HealthStats
{
    public List<DownloadClientHealthDto> DownloadClients { get; set; } = [];

    public List<ArrInstanceHealthDto> ArrInstances { get; set; } = [];
}

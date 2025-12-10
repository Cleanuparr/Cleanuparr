using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;

namespace Cleanuparr.Infrastructure.Features.Arr;

public sealed class ArrClientFactory : IArrClientFactory
{
    private readonly ISonarrClient _sonarrClient;
    private readonly IRadarrClient _radarrClient;
    private readonly ILidarrClient _lidarrClient;
    private readonly IReadarrClient _readarrClient;
    private readonly IWhisparrClient _whisparrClient;

    public ArrClientFactory(
        ISonarrClient sonarrClient,
        IRadarrClient radarrClient,
        ILidarrClient lidarrClient,
        IReadarrClient readarrClient,
        IWhisparrClient whisparrClient
    )
    {
        _sonarrClient = sonarrClient;
        _radarrClient = radarrClient;
        _lidarrClient = lidarrClient;
        _readarrClient = readarrClient;
        _whisparrClient = whisparrClient;
    }
    
    public IArrClient GetClient(InstanceType type) =>
        type switch
        {
            InstanceType.Sonarr => _sonarrClient,
            InstanceType.Radarr => _radarrClient,
            InstanceType.Lidarr => _lidarrClient,
            InstanceType.Readarr => _readarrClient,
            InstanceType.Whisparr => _whisparrClient,
            _ => throw new NotImplementedException($"instance type {type} is not yet supported")
        };
}
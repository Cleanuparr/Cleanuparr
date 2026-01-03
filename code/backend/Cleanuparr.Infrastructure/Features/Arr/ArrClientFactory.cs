using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;

namespace Cleanuparr.Infrastructure.Features.Arr;

public sealed class ArrClientFactory : IArrClientFactory
{
    private readonly ISonarrClient _sonarrClient;
    private readonly IRadarrClient _radarrClient;
    private readonly ILidarrClient _lidarrClient;
    private readonly IReadarrClient _readarrClient;
    private readonly IWhisparrV2Client _whisparrV2Client;
    private readonly IWhisparrV3Client _whisparrV3Client;

    public ArrClientFactory(
        ISonarrClient sonarrClient,
        IRadarrClient radarrClient,
        ILidarrClient lidarrClient,
        IReadarrClient readarrClient,
        IWhisparrV2Client whisparrV2Client,
        IWhisparrV3Client whisparrV3Client
    )
    {
        _sonarrClient = sonarrClient;
        _radarrClient = radarrClient;
        _lidarrClient = lidarrClient;
        _readarrClient = readarrClient;
        _whisparrV2Client = whisparrV2Client;
        _whisparrV3Client = whisparrV3Client;
    }
    
    public IArrClient GetClient(InstanceType type, float instanceVersion) =>
        type switch
        {
            InstanceType.Sonarr => _sonarrClient,
            InstanceType.Radarr => _radarrClient,
            InstanceType.Lidarr => _lidarrClient,
            InstanceType.Readarr => _readarrClient,
            InstanceType.Whisparr when instanceVersion is 2 => _whisparrV2Client,
            InstanceType.Whisparr when instanceVersion is 3 => _whisparrV3Client,
            _ => throw new NotImplementedException($"instance type {type} is not yet supported")
        };
}
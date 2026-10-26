using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Features.Seeker;

public static class SeekerApps
{
    /// <summary>
    /// True for the instances Seeker searches proactively: Sonarr, Radarr, Lidarr and Whisparr v3.
    /// </summary>
    public static bool IsSupported(ArrInstance arrInstance) => arrInstance.ArrConfig.Type switch
    {
        InstanceType.Sonarr or InstanceType.Radarr or InstanceType.Lidarr => true,
        InstanceType.Whisparr => arrInstance.Version is 3,
        _ => false,
    };

    /// <summary>
    /// True for the instances that search one movie per unit: Radarr and Whisparr v3.
    /// </summary>
    public static bool SearchesMovies(ArrInstance arrInstance) => arrInstance.ArrConfig.Type switch
    {
        InstanceType.Radarr => true,
        InstanceType.Whisparr => arrInstance.Version is 3,
        _ => false,
    };
}

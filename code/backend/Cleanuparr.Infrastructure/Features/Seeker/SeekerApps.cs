using System.Globalization;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Features.Seeker;

public static class SeekerApps
{
    /// <summary>
    /// True for the instances Seeker searches proactively: Sonarr, Radarr, Lidarr, Chaptarr and Whisparr.
    /// </summary>
    public static bool IsSupported(ArrInstance arrInstance) => arrInstance.ArrConfig.Type switch
    {
        InstanceType.Sonarr or InstanceType.Radarr or InstanceType.Lidarr or InstanceType.Chaptarr => true,
        InstanceType.Whisparr => arrInstance.Version is 2 or 3,
        _ => false,
    };

    /// <summary>
    /// False for Chaptarr, whose book files carry no custom format score.
    /// </summary>
    public static bool ReadsCustomFormatScores(ArrInstance arrInstance) => arrInstance.ArrConfig.Type is not InstanceType.Chaptarr;

    /// <summary>
    /// True for the instances that search one movie per unit: Radarr and Whisparr v3.
    /// </summary>
    public static bool SearchesMovies(ArrInstance arrInstance) => arrInstance.ArrConfig.Type switch
    {
        InstanceType.Radarr => true,
        InstanceType.Whisparr => arrInstance.Version is 3,
        _ => false,
    };

    /// <summary>
    /// True for Whisparr v2, whose seasons are years and whose scenes have no episode numbers.
    /// </summary>
    public static bool NamesScenesByDate(ArrInstance arrInstance) =>
        arrInstance.ArrConfig.Type is InstanceType.Whisparr && arrInstance.Version is 2;

    /// <summary>
    /// "Site 2024" for Whisparr v2, "Show S01" otherwise.
    /// </summary>
    public static string SeasonName(ArrInstance arrInstance, string seriesTitle, int seasonNumber) =>
        NamesScenesByDate(arrInstance) ? $"{seriesTitle} {seasonNumber}" : $"{seriesTitle} S{seasonNumber:D2}";

    /// <summary>
    /// "Site 2024-05-12" for Whisparr v2, "Show S01E05" otherwise.
    /// </summary>
    public static string EpisodeName(ArrInstance arrInstance, string seriesTitle, int seasonNumber, int episodeNumber, DateTimeOffset? airDateUtc)
    {
        if (!NamesScenesByDate(arrInstance))
        {
            return $"{seriesTitle} S{seasonNumber:D2}E{episodeNumber:D2}";
        }

        return airDateUtc is { } airDate
            ? $"{seriesTitle} {airDate.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
            : SeasonName(arrInstance, seriesTitle, seasonNumber);
    }
}

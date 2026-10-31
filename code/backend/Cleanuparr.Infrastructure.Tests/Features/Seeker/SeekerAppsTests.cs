using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Seeker;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Seeker;

public class SeekerAppsTests
{
    private static ArrInstance Instance(InstanceType type, float version = 0)
    {
        return new ArrInstance
        {
            Name = "Test",
            Url = new Uri("http://arr:1234"),
            ApiKey = "key",
            Version = version,
            ArrConfig = new ArrConfig { Type = type },
        };
    }

    [Theory]
    [InlineData(InstanceType.Sonarr, 0, true)]
    [InlineData(InstanceType.Radarr, 0, true)]
    [InlineData(InstanceType.Whisparr, 2, true)]
    [InlineData(InstanceType.Whisparr, 3, true)]
    [InlineData(InstanceType.Lidarr, 0, true)]
    [InlineData(InstanceType.Lidarr, 2, true)]
    [InlineData(InstanceType.Chaptarr, 0, true)]
    [InlineData(InstanceType.Readarr, 0, false)]
    public void IsSupported_ReturnsTrueOnlyForSonarrRadarrLidarrChaptarrAndWhisparr(InstanceType type, float version, bool expected)
    {
        // Act
        bool supported = SeekerApps.IsSupported(Instance(type, version));

        // Assert
        supported.ShouldBe(expected);
    }

    [Theory]
    [InlineData(InstanceType.Sonarr, true)]
    [InlineData(InstanceType.Radarr, true)]
    [InlineData(InstanceType.Lidarr, true)]
    [InlineData(InstanceType.Whisparr, true)]
    [InlineData(InstanceType.Chaptarr, false)]
    public void ReadsCustomFormatScores_ReturnsFalseOnlyForChaptarr(InstanceType type, bool expected)
    {
        // Act
        bool reads = SeekerApps.ReadsCustomFormatScores(Instance(type, 3));

        // Assert
        reads.ShouldBe(expected);
    }

    [Theory]
    [InlineData(InstanceType.Sonarr, 0, false)]
    [InlineData(InstanceType.Radarr, 0, true)]
    [InlineData(InstanceType.Whisparr, 2, false)]
    [InlineData(InstanceType.Whisparr, 3, true)]
    [InlineData(InstanceType.Lidarr, 0, false)]
    [InlineData(InstanceType.Readarr, 0, false)]
    public void SearchesMovies_ReturnsTrueOnlyForRadarrAndWhisparrV3(InstanceType type, float version, bool expected)
    {
        // Act
        bool searchesMovies = SeekerApps.SearchesMovies(Instance(type, version));

        // Assert
        searchesMovies.ShouldBe(expected);
    }

    [Theory]
    [InlineData(InstanceType.Sonarr, 0, false)]
    [InlineData(InstanceType.Whisparr, 2, true)]
    [InlineData(InstanceType.Whisparr, 3, false)]
    public void NamesScenesByDate_ReturnsTrueOnlyForWhisparrV2(InstanceType type, float version, bool expected)
    {
        // Act
        bool namesScenesByDate = SeekerApps.NamesScenesByDate(Instance(type, version));

        // Assert
        namesScenesByDate.ShouldBe(expected);
    }

    [Theory]
    [InlineData(InstanceType.Sonarr, 0, "Show S01")]
    [InlineData(InstanceType.Whisparr, 2, "Show 1")]
    public void SeasonName_NamesTheSeasonPerApp(InstanceType type, float version, string expected)
    {
        // Act
        string name = SeekerApps.SeasonName(Instance(type, version), "Show", 1);

        // Assert
        name.ShouldBe(expected);
    }

    [Fact]
    public void EpisodeName_Sonarr_UsesSeasonAndEpisodeNumbers()
    {
        // Act
        string name = SeekerApps.EpisodeName(Instance(InstanceType.Sonarr), "Show", 1, 5, DateTimeOffset.UtcNow);

        // Assert
        name.ShouldBe("Show S01E05");
    }

    [Fact]
    public void EpisodeName_WhisparrV2_UsesTheReleaseDate()
    {
        // Act
        string name = SeekerApps.EpisodeName(Instance(InstanceType.Whisparr, 2), "Site", 2024, 0, new DateTimeOffset(2024, 5, 12, 0, 0, 0, TimeSpan.Zero));

        // Assert
        name.ShouldBe("Site 2024-05-12");
    }

    [Fact]
    public void EpisodeName_WhisparrV2WithoutReleaseDate_UsesTheYear()
    {
        // Act
        string name = SeekerApps.EpisodeName(Instance(InstanceType.Whisparr, 2), "Site", 2024, 0, null);

        // Assert
        name.ShouldBe("Site 2024");
    }
}

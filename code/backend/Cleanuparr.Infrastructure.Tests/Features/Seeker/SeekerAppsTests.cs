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
    [InlineData(InstanceType.Whisparr, 2, false)]
    [InlineData(InstanceType.Whisparr, 3, true)]
    [InlineData(InstanceType.Lidarr, 0, false)]
    [InlineData(InstanceType.Readarr, 0, false)]
    public void IsSupported_ReturnsTrueOnlyForSonarrRadarrAndWhisparrV3(InstanceType type, float version, bool expected)
    {
        // Act
        bool supported = SeekerApps.IsSupported(Instance(type, version));

        // Assert
        supported.ShouldBe(expected);
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
}

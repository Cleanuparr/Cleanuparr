using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using NSubstitute;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Arr;

public class ArrClientFactoryTests
{
    private readonly ISonarrClient _sonarrClient;
    private readonly IRadarrClient _radarrClient;
    private readonly ILidarrClient _lidarrClient;
    private readonly IReadarrClient _readarrClient;
    private readonly IWhisparrV2Client _whisparrClient;
    private readonly IWhisparrV3Client _whisparrV3Client;
    private readonly ArrClientFactory _factory;

    public ArrClientFactoryTests()
    {
        _sonarrClient = Substitute.For<ISonarrClient>();
        _radarrClient = Substitute.For<IRadarrClient>();
        _lidarrClient = Substitute.For<ILidarrClient>();
        _readarrClient = Substitute.For<IReadarrClient>();
        _whisparrClient = Substitute.For<IWhisparrV2Client>();
        _whisparrV3Client = Substitute.For<IWhisparrV3Client>();

        _factory = new ArrClientFactory(
            _sonarrClient,
            _radarrClient,
            _lidarrClient,
            _readarrClient,
            _whisparrClient,
            _whisparrV3Client
        );
    }

    #region GetClient Tests

    [Fact]
    public void GetClient_Sonarr_ReturnsSonarrClient()
    {
        // Act
        var result = _factory.GetClient(InstanceType.Sonarr, 0);

        // Assert
        Assert.Same(_sonarrClient, result);
    }

    [Fact]
    public void GetClient_Radarr_ReturnsRadarrClient()
    {
        // Act
        var result = _factory.GetClient(InstanceType.Radarr, 0);

        // Assert
        Assert.Same(_radarrClient, result);
    }

    [Fact]
    public void GetClient_Lidarr_ReturnsLidarrClient()
    {
        // Act
        var result = _factory.GetClient(InstanceType.Lidarr, 0);

        // Assert
        Assert.Same(_lidarrClient, result);
    }

    [Fact]
    public void GetClient_Readarr_ReturnsReadarrClient()
    {
        // Act
        var result = _factory.GetClient(InstanceType.Readarr, 0);

        // Assert
        Assert.Same(_readarrClient, result);
    }

    [Fact]
    public void GetClient_Whisparr_ReturnsWhisparrClient()
    {
        // Act
        var result = _factory.GetClient(InstanceType.Whisparr, 2);

        // Assert
        Assert.Same(_whisparrClient, result);
    }

    [Fact]
    public void GetClient_WhisparrV3_ReturnsWhisparrClient()
    {
        // Act
        var result = _factory.GetClient(InstanceType.Whisparr, 3);

        // Assert
        Assert.Same(_whisparrV3Client, result);
    }

    [Fact]
    public void GetClient_UnsupportedType_ThrowsNotImplementedException()
    {
        // Arrange
        var unsupportedType = (InstanceType)999;

        // Act & Assert
        var exception = Assert.Throws<NotImplementedException>(() => _factory.GetClient(unsupportedType, Arg.Any<float>()));
        Assert.Contains("not yet supported", exception.Message);
        Assert.Contains("999", exception.Message);
    }

    [Theory]
    [MemberData(nameof(InstancesData))]
    public void GetClient_AllSupportedTypes_ReturnsNonNullClient(InstanceType instanceType, float? version)
    {
        // Act
        var result = _factory.GetClient(instanceType, version ?? 0f);

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IArrClient>(result);
    }

    [Theory]
    [MemberData(nameof(InstancesData))]
    public void GetClient_CalledMultipleTimes_ReturnsSameInstance(InstanceType instanceType, float? version)
    {
        // Act
        var result1 = _factory.GetClient(instanceType, version ?? 0f);
        var result2 = _factory.GetClient(instanceType, version ?? 0f);

        // Assert
        Assert.Same(result1, result2);
    }

    public static IEnumerable<object?[]> InstancesData =>
    [
        [InstanceType.Sonarr, null],
        [InstanceType.Radarr, null],
        [InstanceType.Lidarr, null],
        [InstanceType.Readarr, null],
        [InstanceType.Whisparr, 2f],
        [InstanceType.Whisparr, 3f]
    ];

    #endregion
}

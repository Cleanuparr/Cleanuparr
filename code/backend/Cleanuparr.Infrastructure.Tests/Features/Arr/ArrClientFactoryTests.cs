using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using NSubstitute;
using Shouldly;
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
        result.ShouldBeSameAs(_sonarrClient);
    }

    [Fact]
    public void GetClient_Radarr_ReturnsRadarrClient()
    {
        // Act
        var result = _factory.GetClient(InstanceType.Radarr, 0);

        // Assert
        result.ShouldBeSameAs(_radarrClient);
    }

    [Fact]
    public void GetClient_Lidarr_ReturnsLidarrClient()
    {
        // Act
        var result = _factory.GetClient(InstanceType.Lidarr, 0);

        // Assert
        result.ShouldBeSameAs(_lidarrClient);
    }

    [Fact]
    public void GetClient_Readarr_ReturnsReadarrClient()
    {
        // Act
        var result = _factory.GetClient(InstanceType.Readarr, 0);

        // Assert
        result.ShouldBeSameAs(_readarrClient);
    }

    [Fact]
    public void GetClient_Whisparr_ReturnsWhisparrClient()
    {
        // Act
        var result = _factory.GetClient(InstanceType.Whisparr, 2);

        // Assert
        result.ShouldBeSameAs(_whisparrClient);
    }

    [Fact]
    public void GetClient_WhisparrV3_ReturnsWhisparrClient()
    {
        // Act
        var result = _factory.GetClient(InstanceType.Whisparr, 3);

        // Assert
        result.ShouldBeSameAs(_whisparrV3Client);
    }

    [Fact]
    public void GetClient_UnsupportedType_ThrowsNotImplementedException()
    {
        // Arrange
        var unsupportedType = (InstanceType)999;

        // Act & Assert
        var exception = Should.Throw<NotImplementedException>(() => _factory.GetClient(unsupportedType, 0f));
        exception.Message.ShouldContain("not yet supported");
        exception.Message.ShouldContain("999");
    }

    [Theory]
    [MemberData(nameof(InstancesData))]
    public void GetClient_AllSupportedTypes_ReturnsNonNullClient(InstanceType instanceType, float? version)
    {
        // Act
        var result = _factory.GetClient(instanceType, version ?? 0f);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeAssignableTo<IArrClient>();
    }

    [Theory]
    [MemberData(nameof(InstancesData))]
    public void GetClient_CalledMultipleTimes_ReturnsSameInstance(InstanceType instanceType, float? version)
    {
        // Act
        var result1 = _factory.GetClient(instanceType, version ?? 0f);
        var result2 = _factory.GetClient(instanceType, version ?? 0f);

        // Assert
        result1.ShouldBeSameAs(result2);
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

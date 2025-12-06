using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Arr;

public class ArrClientFactoryTests
{
    private readonly Mock<ISonarrClient> _sonarrClientMock;
    private readonly Mock<IRadarrClient> _radarrClientMock;
    private readonly Mock<ILidarrClient> _lidarrClientMock;
    private readonly Mock<IReadarrClient> _readarrClientMock;
    private readonly Mock<IWhisparrClient> _whisparrClientMock;
    private readonly ArrClientFactory _factory;

    public ArrClientFactoryTests()
    {
        _sonarrClientMock = new Mock<ISonarrClient>();
        _radarrClientMock = new Mock<IRadarrClient>();
        _lidarrClientMock = new Mock<ILidarrClient>();
        _readarrClientMock = new Mock<IReadarrClient>();
        _whisparrClientMock = new Mock<IWhisparrClient>();

        _factory = new ArrClientFactory(
            _sonarrClientMock.Object,
            _radarrClientMock.Object,
            _lidarrClientMock.Object,
            _readarrClientMock.Object,
            _whisparrClientMock.Object
        );
    }

    #region GetClient Tests

    [Fact]
    public void GetClient_Sonarr_ReturnsSonarrClient()
    {
        // Act
        var result = _factory.GetClient(InstanceType.Sonarr);

        // Assert
        Assert.Same(_sonarrClientMock.Object, result);
    }

    [Fact]
    public void GetClient_Radarr_ReturnsRadarrClient()
    {
        // Act
        var result = _factory.GetClient(InstanceType.Radarr);

        // Assert
        Assert.Same(_radarrClientMock.Object, result);
    }

    [Fact]
    public void GetClient_Lidarr_ReturnsLidarrClient()
    {
        // Act
        var result = _factory.GetClient(InstanceType.Lidarr);

        // Assert
        Assert.Same(_lidarrClientMock.Object, result);
    }

    [Fact]
    public void GetClient_Readarr_ReturnsReadarrClient()
    {
        // Act
        var result = _factory.GetClient(InstanceType.Readarr);

        // Assert
        Assert.Same(_readarrClientMock.Object, result);
    }

    [Fact]
    public void GetClient_Whisparr_ReturnsWhisparrClient()
    {
        // Act
        var result = _factory.GetClient(InstanceType.Whisparr);

        // Assert
        Assert.Same(_whisparrClientMock.Object, result);
    }

    [Fact]
    public void GetClient_UnsupportedType_ThrowsNotImplementedException()
    {
        // Arrange
        var unsupportedType = (InstanceType)999;

        // Act & Assert
        var exception = Assert.Throws<NotImplementedException>(() => _factory.GetClient(unsupportedType));
        Assert.Contains("not yet supported", exception.Message);
        Assert.Contains("999", exception.Message);
    }

    [Theory]
    [InlineData(InstanceType.Sonarr)]
    [InlineData(InstanceType.Radarr)]
    [InlineData(InstanceType.Lidarr)]
    [InlineData(InstanceType.Readarr)]
    [InlineData(InstanceType.Whisparr)]
    public void GetClient_AllSupportedTypes_ReturnsNonNullClient(InstanceType instanceType)
    {
        // Act
        var result = _factory.GetClient(instanceType);

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IArrClient>(result);
    }

    [Theory]
    [InlineData(InstanceType.Sonarr)]
    [InlineData(InstanceType.Radarr)]
    [InlineData(InstanceType.Lidarr)]
    [InlineData(InstanceType.Readarr)]
    [InlineData(InstanceType.Whisparr)]
    public void GetClient_CalledMultipleTimes_ReturnsSameInstance(InstanceType instanceType)
    {
        // Act
        var result1 = _factory.GetClient(instanceType);
        var result2 = _factory.GetClient(instanceType);

        // Assert
        Assert.Same(result1, result2);
    }

    #endregion
}

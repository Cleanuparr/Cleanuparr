using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.LazyLibrarian;
using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using NSubstitute;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Health;

public class InstanceHealthCheckerTests
{
    private readonly IArrClientFactory _arrClientFactory = Substitute.For<IArrClientFactory>();
    private readonly ILazyLibrarianService _lazyLibrarianService = Substitute.For<ILazyLibrarianService>();
    private readonly InstanceHealthChecker _healthChecker;

    private readonly ArrInstance _instance = new()
    {
        Name = "instance",
        Url = new Uri("http://localhost:8989"),
        ApiKey = "api-key",
        Version = 4,
    };

    public InstanceHealthCheckerTests()
    {
        _healthChecker = new InstanceHealthChecker(_arrClientFactory, _lazyLibrarianService);
    }

    [Fact]
    public async Task CheckAsync_LazyLibrarian_UsesItsOwnService()
    {
        // Act
        await _healthChecker.CheckAsync(InstanceType.LazyLibrarian, _instance);

        // Assert
        await _lazyLibrarianService.Received(1).HealthCheckAsync(_instance);
        _arrClientFactory.DidNotReceive().GetClient(Arg.Any<InstanceType>(), Arg.Any<float>());
    }

    [Theory]
    [InlineData(InstanceType.Sonarr)]
    [InlineData(InstanceType.Radarr)]
    [InlineData(InstanceType.Lidarr)]
    [InlineData(InstanceType.Readarr)]
    [InlineData(InstanceType.Whisparr)]
    [InlineData(InstanceType.Sportarr)]
    public async Task CheckAsync_Arr_UsesTheVersionedClient(InstanceType type)
    {
        // Arrange
        IArrClient client = Substitute.For<IArrClient>();
        _arrClientFactory.GetClient(type, _instance.Version).Returns(client);

        // Act
        await _healthChecker.CheckAsync(type, _instance);

        // Assert
        await client.Received(1).HealthCheckAsync(_instance);
        await _lazyLibrarianService.DidNotReceive().HealthCheckAsync(Arg.Any<ArrInstance>());
    }
}

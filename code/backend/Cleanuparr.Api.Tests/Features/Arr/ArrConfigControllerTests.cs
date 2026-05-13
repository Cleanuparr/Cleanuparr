using Cleanuparr.Api.Features.Arr.Contracts.Requests;
using Cleanuparr.Api.Features.Arr.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Dtos;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Features.Arr;

public class ArrConfigControllerTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly IArrClientFactory _arrClientFactory;
    private readonly IArrClient _arrClient;
    private readonly ArrConfigController _controller;

    public ArrConfigControllerTests()
    {
        _dataContext = ConfigControllerTestDataFactory.CreateDataContext();
        var logger = Substitute.For<ILogger<ArrConfigController>>();
        _arrClientFactory = Substitute.For<IArrClientFactory>();
        _arrClient = Substitute.For<IArrClient>();
        _arrClientFactory.GetClient(Arg.Any<InstanceType>(), Arg.Any<float>()).Returns(_arrClient);
        _controller = new ArrConfigController(logger, _dataContext, _arrClientFactory);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    #region GET configs

    [Theory]
    [InlineData(InstanceType.Sonarr)]
    [InlineData(InstanceType.Radarr)]
    [InlineData(InstanceType.Lidarr)]
    [InlineData(InstanceType.Readarr)]
    [InlineData(InstanceType.Whisparr)]
    public async Task GetArrConfig_AllTypes_ReturnOk(InstanceType type)
    {
        // Act
        var result = await DispatchGet(type);

        // Assert
        var ok = result.ShouldBeOfType<OkObjectResult>();
        var dto = ok.Value.ShouldBeOfType<ArrConfigDto>();
        dto.Type.ShouldBe(type);
    }

    [Fact]
    public async Task GetSonarrConfig_OrdersInstancesByName()
    {
        // Arrange
        var config = await _dataContext.ArrConfigs.FirstAsync(c => c.Type == InstanceType.Sonarr);
        _dataContext.ArrInstances.AddRange(
            new ArrInstance { Name = "z", Url = new Uri("http://z"), ApiKey = "k", ArrConfigId = config.Id, Enabled = true },
            new ArrInstance { Name = "a", Url = new Uri("http://a"), ApiKey = "k", ArrConfigId = config.Id, Enabled = true });
        await _dataContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetSonarrConfig();

        // Assert
        var ok = result.ShouldBeOfType<OkObjectResult>();
        var dto = ok.Value.ShouldBeOfType<ArrConfigDto>();
        dto.Instances[0].Name.ShouldBe("a");
        dto.Instances[1].Name.ShouldBe("z");
    }

    #endregion

    #region PUT configs

    [Fact]
    public async Task UpdateSonarrConfig_PersistsFailedImportMaxStrikes()
    {
        // Arrange
        var request = new UpdateArrConfigRequest { FailedImportMaxStrikes = 7 };

        // Act
        var result = await _controller.UpdateSonarrConfig(request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        var saved = await _dataContext.ArrConfigs.AsNoTracking().FirstAsync(c => c.Type == InstanceType.Sonarr);
        saved.FailedImportMaxStrikes.ShouldBe((short)7);
    }

    [Fact]
    public async Task UpdateSonarrConfig_DefaultStrikes_PassesThrough()
    {
        // ArrConfig.Validate is currently a no-op; -1 (the default disabled value) is accepted
        var request = new UpdateArrConfigRequest { FailedImportMaxStrikes = -1 };

        // Act
        var result = await _controller.UpdateSonarrConfig(request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
    }

    #endregion

    #region Create instance

    [Fact]
    public async Task CreateSonarrInstance_PersistsInstanceUnderSonarrConfig()
    {
        // Arrange
        var request = new ArrInstanceRequest
        {
            Name = "test",
            Url = "http://sonarr.test:8989",
            ApiKey = "abc",
            Version = 4f,
        };

        // Act
        var result = await _controller.CreateSonarrInstance(request);

        // Assert
        var created = result.ShouldBeOfType<CreatedAtActionResult>();
        var dto = created.Value.ShouldBeOfType<ArrInstanceDto>();
        dto.Name.ShouldBe("test");
        var sonarrConfig = await _dataContext.ArrConfigs
            .Include(c => c.Instances)
            .FirstAsync(c => c.Type == InstanceType.Sonarr);
        sonarrConfig.Instances.ShouldContain(i => i.Name == "test");
    }

    [Fact]
    public async Task CreateSonarrInstance_PlaceholderApiKey_ThrowsValidationException()
    {
        // Arrange — placeholder ApiKey is rejected by ArrInstanceRequest.ToEntity
        var request = new ArrInstanceRequest
        {
            Name = "test",
            Url = "http://sonarr.test:8989",
            ApiKey = "••••••••",
            Version = 4f,
        };

        // Act / Assert
        await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.ValidationException>(
            () => _controller.CreateSonarrInstance(request));
    }

    #endregion

    #region Update instance

    [Fact]
    public async Task UpdateSonarrInstance_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var request = new ArrInstanceRequest
        {
            Name = "x",
            Url = "http://x",
            ApiKey = "k",
            Version = 4f,
        };

        // Act
        var result = await _controller.UpdateSonarrInstance(Guid.NewGuid(), request);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateSonarrInstance_Existing_PersistsChanges()
    {
        // Arrange
        var sonarr = await _dataContext.ArrConfigs.FirstAsync(c => c.Type == InstanceType.Sonarr);
        var instance = new ArrInstance
        {
            Name = "orig",
            Url = new Uri("http://orig:8989"),
            ApiKey = "k",
            ArrConfigId = sonarr.Id,
            Enabled = true,
        };
        _dataContext.ArrInstances.Add(instance);
        await _dataContext.SaveChangesAsync();

        var request = new ArrInstanceRequest
        {
            Name = "renamed",
            Url = "http://renamed:8989",
            ApiKey = "newkey",
            Version = 4f,
            Enabled = false,
        };

        // Act
        var result = await _controller.UpdateSonarrInstance(instance.Id, request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        var saved = await _dataContext.ArrInstances.AsNoTracking().FirstAsync(i => i.Id == instance.Id);
        saved.Name.ShouldBe("renamed");
        saved.Enabled.ShouldBeFalse();
        saved.ApiKey.ShouldBe("newkey");
    }

    #endregion

    #region Delete instance

    [Fact]
    public async Task DeleteSonarrInstance_UnknownId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.DeleteSonarrInstance(Guid.NewGuid());

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteSonarrInstance_Existing_ReturnsNoContent()
    {
        // Arrange
        var sonarr = await _dataContext.ArrConfigs.FirstAsync(c => c.Type == InstanceType.Sonarr);
        var instance = new ArrInstance
        {
            Name = "doomed",
            Url = new Uri("http://doomed:8989"),
            ApiKey = "k",
            ArrConfigId = sonarr.Id,
            Enabled = true,
        };
        _dataContext.ArrInstances.Add(instance);
        await _dataContext.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteSonarrInstance(instance.Id);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        (await _dataContext.ArrInstances.CountAsync(i => i.Id == instance.Id)).ShouldBe(0);
    }

    #endregion

    #region Test instance

    [Fact]
    public async Task TestSonarrInstance_HealthCheckSucceeds_ReturnsOk()
    {
        // Arrange — IArrClient.HealthCheckAsync returns Task.CompletedTask by default
        var request = new TestArrInstanceRequest
        {
            Url = "http://sonarr.test:8989",
            ApiKey = "k",
            Version = 4f,
        };

        // Act
        var result = await _controller.TestSonarrInstance(request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        await _arrClient.Received(1).HealthCheckAsync(Arg.Any<ArrInstance>());
    }

    [Fact]
    public async Task TestSonarrInstance_HealthCheckThrows_ReturnsBadRequest()
    {
        // Arrange
        _arrClient.HealthCheckAsync(Arg.Any<ArrInstance>())
            .Returns(Task.FromException(new HttpRequestException("unreachable")));

        var request = new TestArrInstanceRequest
        {
            Url = "http://sonarr.test:8989",
            ApiKey = "k",
            Version = 4f,
        };

        // Act
        var result = await _controller.TestSonarrInstance(request);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TestSonarrInstance_PlaceholderApiKeyNoInstanceId_ReturnsBadRequest()
    {
        // Arrange — placeholder API key with no InstanceId means we can't resolve it; ToTestInstance throws
        var request = new TestArrInstanceRequest
        {
            Url = "http://sonarr.test:8989",
            ApiKey = "••••••••",
            Version = 4f,
        };

        // Act
        var result = await _controller.TestSonarrInstance(request);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TestSonarrInstance_PlaceholderApiKeyResolvesFromInstanceId_RunsCheck()
    {
        // Arrange
        var sonarr = await _dataContext.ArrConfigs.FirstAsync(c => c.Type == InstanceType.Sonarr);
        var stored = new ArrInstance
        {
            Name = "stored",
            Url = new Uri("http://stored:8989"),
            ApiKey = "stored-key",
            ArrConfigId = sonarr.Id,
            Enabled = true,
        };
        _dataContext.ArrInstances.Add(stored);
        await _dataContext.SaveChangesAsync();

        var request = new TestArrInstanceRequest
        {
            Url = "http://sonarr.test:8989",
            ApiKey = "••••••••",
            Version = 4f,
            InstanceId = stored.Id,
        };

        // Act
        var result = await _controller.TestSonarrInstance(request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        await _arrClient.Received(1).HealthCheckAsync(Arg.Is<ArrInstance>(i => i.ApiKey == "stored-key"));
    }

    #endregion

    private Task<IActionResult> DispatchGet(InstanceType type) => type switch
    {
        InstanceType.Sonarr => _controller.GetSonarrConfig(),
        InstanceType.Radarr => _controller.GetRadarrConfig(),
        InstanceType.Lidarr => _controller.GetLidarrConfig(),
        InstanceType.Readarr => _controller.GetReadarrConfig(),
        InstanceType.Whisparr => _controller.GetWhisparrConfig(),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}

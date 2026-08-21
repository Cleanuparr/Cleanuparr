using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Api.Features.Arr.Contracts.Requests;
using Cleanuparr.Api.Features.Arr.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
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
    private readonly EventsContext _eventsContext;
    private readonly IInstanceHealthChecker _healthChecker;
    private readonly IEventPublisher _eventPublisher;
    private readonly ArrConfigController _controller;

    public ArrConfigControllerTests()
    {
        _dataContext = ConfigControllerTestDataFactory.CreateDataContext();
        _eventsContext = ConfigControllerTestDataFactory.CreateEventsContext();
        var logger = Substitute.For<ILogger<ArrConfigController>>();
        _healthChecker = Substitute.For<IInstanceHealthChecker>();
        _eventPublisher = Substitute.For<IEventPublisher>();
        _controller = new ArrConfigController(logger, _dataContext, _eventsContext, _healthChecker, _eventPublisher);
        ConfigControllerTestDataFactory.ConfigureProblemDetails(_controller);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        _eventsContext.Dispose();
        GC.SuppressFinalize(this);
    }

    #region GET configs

    [Theory]
    [InlineData(InstanceType.Sonarr)]
    [InlineData(InstanceType.Radarr)]
    [InlineData(InstanceType.Lidarr)]
    [InlineData(InstanceType.Readarr)]
    [InlineData(InstanceType.Whisparr)]
    [InlineData(InstanceType.Sportarr)]
    [InlineData(InstanceType.LazyLibrarian)]
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
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(404);
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
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(404);
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

    [Fact]
    public async Task DeleteSonarrInstance_FailsSearchEventsThatAreStillInFlight()
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
        await _controller.DeleteSonarrInstance(instance.Id);

        // Assert
        await _eventPublisher.Received(1).FailStrandedSearchEvents(instance.Id);
    }

    [Fact]
    public async Task DeleteSonarrInstance_WhenStateCleanupFails_KeepsInstance()
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

        // Force the events cleanup to fail so the instance delete must roll back
        await _eventsContext.DisposeAsync();

        // Act
        await Should.ThrowAsync<Exception>(() => _controller.DeleteSonarrInstance(instance.Id));

        // Assert — instance is preserved (never deleted without its events state being removed)
        (await _dataContext.ArrInstances.AsNoTracking().CountAsync(i => i.Id == instance.Id)).ShouldBe(1);
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
        await _healthChecker.Received(1).CheckAsync(InstanceType.Sonarr, Arg.Any<ArrInstance>());
    }

    [Fact]
    public async Task TestSonarrInstance_HealthCheckThrows_ReturnsBadRequest()
    {
        // Arrange
        _healthChecker.CheckAsync(Arg.Any<InstanceType>(), Arg.Any<ArrInstance>())
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
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(400);
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
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(400);
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
        await _healthChecker.Received(1).CheckAsync(InstanceType.Sonarr, Arg.Is<ArrInstance>(i => i.ApiKey == "stored-key"));
    }

    #endregion

    #region Route wiring

    [Theory]
    [InlineData(InstanceType.Sonarr)]
    [InlineData(InstanceType.Radarr)]
    [InlineData(InstanceType.Lidarr)]
    [InlineData(InstanceType.Readarr)]
    [InlineData(InstanceType.Whisparr)]
    [InlineData(InstanceType.Sportarr)]
    [InlineData(InstanceType.LazyLibrarian)]
    public async Task InstanceRoutes_AllTypes_CreateUpdateDeleteAndTest(InstanceType type)
    {
        // Arrange: every route delegates to the same helper, so this covers the wiring per type.
        ArrInstanceRequest request = new()
        {
            Name = "wired",
            Url = "http://instance.test:1234",
            ApiKey = "abc",
            Version = 1f,
        };

        // Act + Assert: create
        IActionResult created = await DispatchCreate(type, request);
        ArrInstanceDto dto = created.ShouldBeOfType<CreatedAtActionResult>().Value.ShouldBeOfType<ArrInstanceDto>();

        // Act + Assert: update
        Guid id = dto.Id.ShouldNotBeNull();
        IActionResult updated = await DispatchUpdate(type, id, request with { Name = "rewired" });
        updated.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<ArrInstanceDto>().Name.ShouldBe("rewired");

        // Act + Assert: connection test
        IActionResult tested = await DispatchTest(type, new TestArrInstanceRequest
        {
            Url = request.Url,
            ApiKey = request.ApiKey,
            Version = request.Version,
        });
        tested.ShouldBeOfType<OkObjectResult>();
        await _healthChecker.Received(1).CheckAsync(type, Arg.Any<ArrInstance>());

        // Act + Assert: delete
        IActionResult deleted = await DispatchDelete(type, id);
        deleted.ShouldBeOfType<NoContentResult>();

        ArrConfig config = await _dataContext.ArrConfigs
            .Include(c => c.Instances)
            .FirstAsync(c => c.Type == type);
        config.Instances.ShouldBeEmpty();
    }

    #endregion

    private Task<IActionResult> DispatchCreate(InstanceType type, ArrInstanceRequest request) => type switch
    {
        InstanceType.Sonarr => _controller.CreateSonarrInstance(request),
        InstanceType.Radarr => _controller.CreateRadarrInstance(request),
        InstanceType.Lidarr => _controller.CreateLidarrInstance(request),
        InstanceType.Readarr => _controller.CreateReadarrInstance(request),
        InstanceType.Whisparr => _controller.CreateWhisparrInstance(request),
        InstanceType.Sportarr => _controller.CreateSportarrInstance(request),
        InstanceType.LazyLibrarian => _controller.CreateLazyLibrarianInstance(request),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private Task<IActionResult> DispatchUpdate(InstanceType type, Guid id, ArrInstanceRequest request) => type switch
    {
        InstanceType.Sonarr => _controller.UpdateSonarrInstance(id, request),
        InstanceType.Radarr => _controller.UpdateRadarrInstance(id, request),
        InstanceType.Lidarr => _controller.UpdateLidarrInstance(id, request),
        InstanceType.Readarr => _controller.UpdateReadarrInstance(id, request),
        InstanceType.Whisparr => _controller.UpdateWhisparrInstance(id, request),
        InstanceType.Sportarr => _controller.UpdateSportarrInstance(id, request),
        InstanceType.LazyLibrarian => _controller.UpdateLazyLibrarianInstance(id, request),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private Task<IActionResult> DispatchDelete(InstanceType type, Guid id) => type switch
    {
        InstanceType.Sonarr => _controller.DeleteSonarrInstance(id),
        InstanceType.Radarr => _controller.DeleteRadarrInstance(id),
        InstanceType.Lidarr => _controller.DeleteLidarrInstance(id),
        InstanceType.Readarr => _controller.DeleteReadarrInstance(id),
        InstanceType.Whisparr => _controller.DeleteWhisparrInstance(id),
        InstanceType.Sportarr => _controller.DeleteSportarrInstance(id),
        InstanceType.LazyLibrarian => _controller.DeleteLazyLibrarianInstance(id),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private Task<IActionResult> DispatchTest(InstanceType type, TestArrInstanceRequest request) => type switch
    {
        InstanceType.Sonarr => _controller.TestSonarrInstance(request),
        InstanceType.Radarr => _controller.TestRadarrInstance(request),
        InstanceType.Lidarr => _controller.TestLidarrInstance(request),
        InstanceType.Readarr => _controller.TestReadarrInstance(request),
        InstanceType.Whisparr => _controller.TestWhisparrInstance(request),
        InstanceType.Sportarr => _controller.TestSportarrInstance(request),
        InstanceType.LazyLibrarian => _controller.TestLazyLibrarianInstance(request),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private Task<IActionResult> DispatchGet(InstanceType type) => type switch
    {
        InstanceType.Sonarr => _controller.GetSonarrConfig(),
        InstanceType.Radarr => _controller.GetRadarrConfig(),
        InstanceType.Lidarr => _controller.GetLidarrConfig(),
        InstanceType.Readarr => _controller.GetReadarrConfig(),
        InstanceType.Whisparr => _controller.GetWhisparrConfig(),
        InstanceType.Sportarr => _controller.GetSportarrConfig(),
        InstanceType.LazyLibrarian => _controller.GetLazyLibrarianConfig(),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}

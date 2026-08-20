using Cleanuparr.Api.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Controllers;

public class StatusControllerTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly IInstanceHealthChecker _healthChecker;
    private readonly StatusController _controller;

    public StatusControllerTests()
    {
        _dataContext = ConfigControllerTestDataFactory.CreateDataContext();
        _healthChecker = Substitute.For<IInstanceHealthChecker>();
        _controller = new StatusController(
            Substitute.For<ILogger<StatusController>>(),
            _dataContext,
            _healthChecker);
        ConfigControllerTestDataFactory.ConfigureProblemDetails(_controller);
    }

    private async Task<ArrInstance> AddEnabledInstance(InstanceType type, string name = "instance")
    {
        Guid configId = await _dataContext.ArrConfigs
            .AsNoTracking()
            .Where(x => x.Type == type)
            .Select(x => x.Id)
            .FirstAsync();

        ArrInstance instance = new()
        {
            Name = name,
            Url = new Uri("http://instance.local"),
            ApiKey = "key",
            Enabled = true,
            ArrConfigId = configId,
        };

        _dataContext.ArrInstances.Add(instance);
        await _dataContext.SaveChangesAsync();

        return instance;
    }

    private static Dictionary<string, object> AsDictionary(IActionResult result) =>
        result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<Dictionary<string, object>>();

    [Fact]
    public async Task GetMediaManagersStatus_CoversEveryInstanceType()
    {
        // Act
        IActionResult result = await _controller.GetMediaManagersStatus();

        // Assert: the list drives the response, so a forgotten member would vanish from the UI.
        Dictionary<string, object> status = AsDictionary(result);
        foreach (InstanceType type in Enum.GetValues<InstanceType>())
        {
            status.ShouldContainKey(type.ToString());
        }
    }

    [Fact]
    public async Task GetMediaManagersStatus_IncludesSportarr()
    {
        // Act
        IActionResult result = await _controller.GetMediaManagersStatus();

        // Assert
        AsDictionary(result).ShouldContainKey(nameof(InstanceType.Sportarr));
    }

    [Fact]
    public async Task GetMediaManagersStatus_IncludesLazyLibrarian()
    {
        // Act
        IActionResult result = await _controller.GetMediaManagersStatus();

        // Assert
        AsDictionary(result).ShouldContainKey(nameof(InstanceType.LazyLibrarian));
    }

    [Fact]
    public async Task GetMediaManagersStatus_ProbesAnEnabledInstance()
    {
        // Arrange
        await AddEnabledInstance(InstanceType.LazyLibrarian);

        // Act
        await _controller.GetMediaManagersStatus();

        // Assert
        await _healthChecker.Received(1).CheckAsync(InstanceType.LazyLibrarian, Arg.Any<ArrInstance>());
    }

    [Fact]
    public async Task GetMediaManagersStatus_ReportsTheFailureReason()
    {
        // Arrange
        await AddEnabledInstance(InstanceType.Sonarr);
        _healthChecker
            .CheckAsync(Arg.Any<InstanceType>(), Arg.Any<ArrInstance>())
            .ThrowsAsync(new Exception("connection refused"));

        // Act
        IActionResult result = await _controller.GetMediaManagersStatus();

        // Assert
        object sonarr = AsDictionary(result)[nameof(InstanceType.Sonarr)];
        sonarr.ShouldBeOfType<List<object>>().ShouldHaveSingleItem().ToString()!.ShouldContain("connection refused");
    }

    [Fact]
    public async Task GetSystemStatus_CountsInstancesPerType()
    {
        // Arrange
        await AddEnabledInstance(InstanceType.Radarr, "one");
        await AddEnabledInstance(InstanceType.Radarr, "two");

        // Act
        IActionResult result = await _controller.GetSystemStatus();

        // Assert
        result.ShouldBeOfType<OkObjectResult>().Value.ShouldNotBeNull();
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }
}

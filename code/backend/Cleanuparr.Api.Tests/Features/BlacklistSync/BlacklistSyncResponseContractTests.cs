using Cleanuparr.Api.Features.BlacklistSync.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Features.BlacklistSync;

/// <summary>
/// Records today's wire shape, leaks included.
/// </summary>
public class BlacklistSyncResponseContractTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly BlacklistSyncConfigController _controller;

    public BlacklistSyncResponseContractTests()
    {
        _dataContext = ConfigControllerTestDataFactory.CreateDataContext();

        _controller = new BlacklistSyncConfigController(
            Substitute.For<ILogger<BlacklistSyncConfigController>>(),
            _dataContext,
            Substitute.For<IJobManagementService>());
        ConfigControllerTestDataFactory.ConfigureProblemDetails(_controller);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetBlacklistSyncConfig_ReturnsTheDocumentedKeys()
    {
        IActionResult result = await _controller.GetBlacklistSyncConfig();

        ResponseContract.Keys(result).ShouldBe(
        [
            "blacklistPath",
            "cronExpression",
            "enabled",
            "id",
        ]);
    }
}

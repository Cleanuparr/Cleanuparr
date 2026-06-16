using Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;
using Cleanuparr.Api.Features.DownloadCleaner.Contracts.Responses;
using Cleanuparr.Api.Features.DownloadCleaner.Controllers;
using Cleanuparr.Api.Tests.Features.DownloadCleaner.TestHelpers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Api.Tests.Features.DownloadCleaner;

public class DeadTorrentConfigControllerTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly DeadTorrentConfigController _controller;

    public DeadTorrentConfigControllerTests()
    {
        _dataContext = SeedingRulesTestDataFactory.CreateDataContext();
        var logger = Substitute.For<ILogger<DeadTorrentConfigController>>();
        _controller = new DeadTorrentConfigController(logger, _dataContext);
        ControllerTestContext.Attach(_controller);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private static DeadTorrentConfigRequest ValidRequest(
        bool enabled = true,
        string targetCategory = "cleanuparr-dead",
        bool useTag = false,
        ushort maxStrikes = 3,
        List<string>? categories = null)
        => new()
        {
            Enabled = enabled,
            TargetCategory = targetCategory,
            UseTag = useTag,
            MaxStrikes = maxStrikes,
            Categories = categories ?? ["movies"],
        };

    [Fact]
    public async Task Update_ValidRequest_PersistsConfig()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);

        var result = await _controller.UpdateDeadTorrentConfig(client.Id, ValidRequest(maxStrikes: 5, categories: ["movies", "tv"]));

        result.ShouldBeOfType<OkObjectResult>();
        var saved = await _dataContext.DeadTorrentConfigs.AsNoTracking().SingleAsync(d => d.DownloadClientConfigId == client.Id);
        saved.Enabled.ShouldBeTrue();
        saved.MaxStrikes.ShouldBe((ushort)5);
        saved.Categories.ShouldBe(new List<string> { "movies", "tv" });
    }

    [Fact]
    public async Task Update_ThenGet_RoundTrips()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        await _controller.UpdateDeadTorrentConfig(client.Id, ValidRequest(useTag: true, maxStrikes: 4));

        var result = await _controller.GetDeadTorrentConfig(client.Id);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var config = ok.Value.ShouldBeOfType<DeadTorrentConfigResponse>();
        config.UseTag.ShouldBeTrue();
        config.MaxStrikes.ShouldBe((ushort)4);
    }

    [Fact]
    public async Task Update_StrikesBelowMinimum_ThrowsValidationException()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);

        await Should.ThrowAsync<ValidationException>(() => _controller.UpdateDeadTorrentConfig(client.Id, ValidRequest(maxStrikes: 2)));
    }

    [Fact]
    public async Task Update_EnabledForRTorrent_ReturnsBadRequest()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext, DownloadClientTypeName.rTorrent, "Test rTorrent");

        var result = await _controller.UpdateDeadTorrentConfig(client.Id, ValidRequest());

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Update_NonExistentClient_ReturnsNotFound()
    {
        var result = await _controller.UpdateDeadTorrentConfig(Guid.NewGuid(), ValidRequest());

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }
}

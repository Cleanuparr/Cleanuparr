using Cleanuparr.Api.Features.Webhooks.Contracts;
using Cleanuparr.Api.Features.Webhooks.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Cleanuparr.Api.Tests.Features.Webhooks;

public class WebhooksControllerTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly IJobManagementService _jobManagement;
    private readonly WebhooksController _controller;

    private Guid _sonarrInstanceId;
    private Guid _lidarrInstanceId;

    public WebhooksControllerTests()
    {
        _dataContext = CreateDataContext();
        _jobManagement = Substitute.For<IJobManagementService>();
        var logger = Substitute.For<ILogger<WebhooksController>>();
        _controller = new WebhooksController(logger, _dataContext, _jobManagement);
        ControllerTestContext.Attach(_controller);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private DataContext CreateDataContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<DataContext>().UseSqlite(connection).Options;
        var context = new DataContext(options);
        context.Database.EnsureCreated();

        var sonarrInstance = new ArrInstance { Enabled = true, Name = "Sonarr", Url = new Uri("http://sonarr:8989"), ApiKey = "key" };
        var lidarrInstance = new ArrInstance { Enabled = true, Name = "Lidarr", Url = new Uri("http://lidarr:8686"), ApiKey = "key" };
        _sonarrInstanceId = sonarrInstance.Id;
        _lidarrInstanceId = lidarrInstance.Id;

        context.ArrConfigs.AddRange(
            new ArrConfig { Type = InstanceType.Sonarr, Instances = [sonarrInstance] },
            new ArrConfig { Type = InstanceType.Lidarr, Instances = [lidarrInstance] }
        );

        context.ContentBlockerConfigs.Add(new ContentBlockerConfig
        {
            Enabled = true,
            TriggerMode = JobTriggerMode.Both,
            IgnoredDownloads = [],
        });

        context.SaveChanges();
        return context;
    }

    private void SetConfig(bool enabled, JobTriggerMode mode)
    {
        var config = _dataContext.ContentBlockerConfigs.First();
        config.Enabled = enabled;
        config.TriggerMode = mode;
        _dataContext.SaveChanges();
    }

    private static ArrWebhookPayload GrabPayload(string? downloadId = "HASH123", long seriesId = 42) => new()
    {
        EventType = "Grab",
        DownloadId = downloadId,
        Series = new ArrWebhookContent { Id = seriesId },
    };

    [Fact]
    public async Task TestEvent_ReturnsOk_AndDoesNotSchedule()
    {
        var result = await _controller.TriggerMalwareBlocker(_sonarrInstanceId, new ArrWebhookPayload { EventType = "Test" });

        result.ShouldBeOfType<OkResult>();
        await _jobManagement.DidNotReceive()
            .TriggerMalwareBlockerWebhook(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<InstanceType>());
    }

    [Fact]
    public async Task ValidGrab_SchedulesTargetedScan()
    {
        var result = await _controller.TriggerMalwareBlocker(_sonarrInstanceId, GrabPayload());

        result.ShouldBeOfType<OkResult>();
        await _jobManagement.Received(1)
            .TriggerMalwareBlockerWebhook(_sonarrInstanceId, "HASH123", 42, InstanceType.Sonarr);
    }

    [Fact]
    public async Task UnknownInstance_ReturnsNotFound()
    {
        var result = await _controller.TriggerMalwareBlocker(Guid.NewGuid(), GrabPayload());

        var notFound = result.ShouldBeOfType<ObjectResult>();
        notFound.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        notFound.Value.ShouldBeOfType<ProblemDetails>();
        await _jobManagement.DidNotReceive()
            .TriggerMalwareBlockerWebhook(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<InstanceType>());
    }

    [Fact]
    public async Task NonSonarrRadarrInstance_ReturnsUnprocessable()
    {
        var result = await _controller.TriggerMalwareBlocker(_lidarrInstanceId, GrabPayload());

        var unprocessable = result.ShouldBeOfType<ObjectResult>();
        unprocessable.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        unprocessable.Value.ShouldBeOfType<ProblemDetails>();
        await _jobManagement.DidNotReceive()
            .TriggerMalwareBlockerWebhook(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<InstanceType>());
    }

    [Fact]
    public async Task Disabled_ReturnsOk_AndDoesNotSchedule()
    {
        SetConfig(enabled: false, JobTriggerMode.Both);

        var result = await _controller.TriggerMalwareBlocker(_sonarrInstanceId, GrabPayload());

        result.ShouldBeOfType<OkResult>();
        await _jobManagement.DidNotReceive()
            .TriggerMalwareBlockerWebhook(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<InstanceType>());
    }

    [Fact]
    public async Task ScheduleOnlyMode_ReturnsOk_AndDoesNotSchedule()
    {
        SetConfig(enabled: true, JobTriggerMode.Schedule);

        var result = await _controller.TriggerMalwareBlocker(_sonarrInstanceId, GrabPayload());

        result.ShouldBeOfType<OkResult>();
        await _jobManagement.DidNotReceive()
            .TriggerMalwareBlockerWebhook(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<InstanceType>());
    }

    [Fact]
    public async Task EmptyDownloadId_ReturnsOk_AndDoesNotSchedule()
    {
        var result = await _controller.TriggerMalwareBlocker(_sonarrInstanceId, GrabPayload(downloadId: null));

        result.ShouldBeOfType<OkResult>();
        await _jobManagement.DidNotReceive()
            .TriggerMalwareBlockerWebhook(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<InstanceType>());
    }
}

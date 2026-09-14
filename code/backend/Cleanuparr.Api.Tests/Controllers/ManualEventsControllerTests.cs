using Cleanuparr.Api.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Controllers;

public sealed class ManualEventsControllerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly EventsContext _context;
    private readonly ManualEventsController _controller;

    public ManualEventsControllerTests()
    {
        _context = ConfigControllerTestDataFactory.CreateEventsContext();
        _controller = new ManualEventsController(_context, new FakeTimeProvider(Now));
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<ManualEvent> AddEvent(string message)
    {
        ManualEvent manualEvent = new()
        {
            Type = ManualEventType.RecurringDownload,
            Message = message,
            Severity = EventSeverity.Warning,
            Timestamp = DateTimeOffset.UtcNow.AddDays(-1),
        };

        _context.ManualEvents.Add(manualEvent);
        await _context.SaveChangesAsync();

        return manualEvent;
    }

    [Fact]
    public async Task ResolveManualEvent_StampsTheResolutionTime()
    {
        ManualEvent manualEvent = await AddEvent("one");

        ActionResult result = await _controller.ResolveManualEvent(manualEvent.Id);

        result.ShouldBeOfType<OkResult>();

        ManualEvent stored = await _context.ManualEvents.AsNoTracking().SingleAsync();
        stored.IsResolved.ShouldBeTrue();
        stored.ResolvedAt.ShouldNotBeNull();
        stored.ResolvedAt!.Value.ShouldBe(Now);
    }

    [Fact]
    public async Task ResolveManualEvent_WithUnknownId_ReturnsNotFound()
    {
        ActionResult result = await _controller.ResolveManualEvent(Guid.NewGuid());

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ResolveAllManualEvents_StampsEveryUnresolvedEvent()
    {
        await AddEvent("one");
        await AddEvent("two");

        ActionResult<object> result = await _controller.ResolveAllManualEvents();

        result.Result.ShouldBeOfType<OkObjectResult>();

        List<ManualEvent> stored = await _context.ManualEvents.AsNoTracking().ToListAsync();
        stored.Count.ShouldBe(2);
        stored.ShouldAllBe(e => e.IsResolved);
        stored.ShouldAllBe(e => e.ResolvedAt != null);
        stored[0].ResolvedAt!.Value.ShouldBe(Now);
        stored[1].ResolvedAt!.Value.ShouldBe(stored[0].ResolvedAt!.Value);
    }
}

using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Persistence;

public class LikeParityTests
{
    [Fact]
    public async Task Search_is_case_insensitive_on_both_operands()
    {
        using EventsContext context = TestEventsContextFactory.Create();
        context.Events.Add(new AppEvent
        {
            EventType = EventType.SearchTriggered,
            Message = "Mixed CASE Title",
            Severity = EventSeverity.Information,
        });
        await context.SaveChangesAsync();

        string pattern = EventsContext.GetLikePattern("mixed case");
        int matches = await context.Events
            .CountAsync(e => EF.Functions.Like(e.Message.ToLower(), pattern));

        matches.ShouldBe(1);
    }

    [Fact]
    public async Task Literal_percent_in_search_term_is_escaped_not_wildcard()
    {
        using EventsContext context = TestEventsContextFactory.Create();
        context.Events.Add(new AppEvent
        {
            EventType = EventType.SearchTriggered,
            Message = "discount 50% off",
            Severity = EventSeverity.Information,
        });
        context.Events.Add(new AppEvent
        {
            EventType = EventType.SearchTriggered,
            Message = "discount 500 off",
            Severity = EventSeverity.Information,
        });
        await context.SaveChangesAsync();

        string pattern = EventsContext.GetLikePattern("50%");
        int matches = await context.Events
            .CountAsync(e => EF.Functions.Like(e.Message.ToLower(), pattern, "\\"));

        matches.ShouldBe(1);
    }
}

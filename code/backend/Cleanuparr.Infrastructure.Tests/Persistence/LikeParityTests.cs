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
}

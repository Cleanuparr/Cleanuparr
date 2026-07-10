using Cleanuparr.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace Cleanuparr.Api.DesignTime;

public sealed class EventsContextFactory : IDesignTimeDbContextFactory<EventsContext>
{
    public EventsContext CreateDbContext(string[] args) => EventsContext.CreateStaticInstance();
}

namespace Cleanuparr.Persistence.Models.Events;

public interface IEvent
{
    Guid Id { get; set; }

    DateTimeOffset Timestamp { get; set; }
}
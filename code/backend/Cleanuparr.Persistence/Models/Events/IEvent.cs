namespace Cleanuparr.Persistence.Models.Events;

public interface IEvent
{
    Guid Id { get; set; }
    
    DateTime Timestamp { get; set; }
    
    /// <summary>
    /// JSON data associated with the event
    /// </summary>
    string? Data { get; set; }
}
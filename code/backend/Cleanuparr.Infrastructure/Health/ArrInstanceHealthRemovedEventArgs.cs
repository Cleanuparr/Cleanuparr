namespace Cleanuparr.Infrastructure.Health;

/// <summary>
/// Event arguments for an arr instance dropped from the health cache
/// </summary>
public class ArrInstanceHealthRemovedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the arr instance ID
    /// </summary>
    public Guid InstanceId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrInstanceHealthRemovedEventArgs"/> class
    /// </summary>
    /// <param name="instanceId">The arr instance ID</param>
    public ArrInstanceHealthRemovedEventArgs(Guid instanceId)
    {
        InstanceId = instanceId;
    }
}

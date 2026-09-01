namespace Cleanuparr.Infrastructure.Health;

/// <summary>
/// Event arguments for a client dropped from the health cache
/// </summary>
public class ClientHealthRemovedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the client ID
    /// </summary>
    public Guid ClientId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientHealthRemovedEventArgs"/> class
    /// </summary>
    /// <param name="clientId">The client ID</param>
    public ClientHealthRemovedEventArgs(Guid clientId)
    {
        ClientId = clientId;
    }
}

namespace Cleanuparr.Infrastructure.Features.Auth;

/// <summary>
/// PIN-based authentication credentials from Plex.
/// </summary>
public sealed record PlexPinResult
{
    /// <summary>
    /// Unique identifier for this PIN request.
    /// </summary>
    public required int PinId { get; init; }

    /// <summary>
    /// Six-digit code displayed to the user for authentication.
    /// </summary>
    public required string PinCode { get; init; }

    /// <summary>
    /// URL where the user authenticates with the PIN code.
    /// </summary>
    public required string AuthUrl { get; init; }
}

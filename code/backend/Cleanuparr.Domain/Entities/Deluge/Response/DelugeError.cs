using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Deluge.Response;

/// <summary>
/// The error of a response from Deluge.
/// </summary>
public sealed record DelugeError
{
    /// <summary>
    /// The text of the error.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The code of the error.
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }
}

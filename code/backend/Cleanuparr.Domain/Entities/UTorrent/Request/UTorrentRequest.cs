namespace Cleanuparr.Domain.Entities.UTorrent.Request;

/// <summary>
/// Represents a request to the µTorrent Web UI API
/// </summary>
public sealed class UTorrentRequest
{
    /// <summary>
    /// The API action to perform
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Authentication token (required for CSRF protection)
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Additional parameters for the request
    /// </summary>
    public List<(string Name, string Value)> Parameters { get; set; } = new();

    /// <summary>
    /// Constructs the query string for the API call
    /// </summary>
    /// <returns>The complete query string including token and action</returns>
    public string ToQueryString()
    {
        var queryParams = new List<string>
        {
            $"token={Token}",
            Action
        };

        foreach (var param in Parameters)
        {
            queryParams.Add($"{Uri.EscapeDataString(param.Name)}={Uri.EscapeDataString(param.Value)}");
        }

        return string.Join("&", queryParams);
    }

    /// <summary>
    /// Creates a new request with the specified action
    /// </summary>
    /// <param name="action">The API action</param>
    /// <param name="token">Authentication token</param>
    /// <returns>A new UTorrentRequest instance</returns>
    public static UTorrentRequest Create(string action, string token)
    {
        return new UTorrentRequest
        {
            Action = action,
            Token = token
        };
    }

    /// <summary>
    /// Adds a parameter to the request
    /// </summary>
    /// <param name="key">Parameter name</param>
    /// <param name="value">Parameter value</param>
    /// <returns>This instance for method chaining</returns>
    public UTorrentRequest WithParameter(string key, string value)
    {
        Parameters.Add((key, value));
        return this;
    }
} 
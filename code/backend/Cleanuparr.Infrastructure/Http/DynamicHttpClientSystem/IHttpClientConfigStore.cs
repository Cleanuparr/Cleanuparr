namespace Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;

/// <summary>
/// Store interface for managing HttpClient configurations dynamically
/// </summary>
public interface IHttpClientConfigStore
{
    /// <summary>
    /// Tries to get a configuration for the specified client name
    /// </summary>
    bool TryGetConfiguration(string clientName, out HttpClientConfig config);
    
    /// <summary>
    /// Adds or updates a configuration for the specified client name
    /// </summary>
    void AddConfiguration(string clientName, HttpClientConfig config);
    
    /// <summary>
    /// Removes a configuration for the specified client name
    /// </summary>
    void RemoveConfiguration(string clientName);
    
    /// <summary>
    /// Gets all currently registered configurations
    /// </summary>
    IEnumerable<KeyValuePair<string, HttpClientConfig>> GetAllConfigurations();
    
    /// <summary>
    /// Updates multiple configurations atomically
    /// </summary>
    void UpdateConfigurations(IEnumerable<KeyValuePair<string, HttpClientConfig>> configurations);
} 
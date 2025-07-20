using System.Collections.Concurrent;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

/// <summary>
/// Implementation of µTorrent authentication management with IMemoryCache-based token sharing
/// Handles concurrent authentication requests and provides thread-safe token caching per client
/// </summary>
public class UTorrentAuthenticator : IUTorrentAuthenticator
{
    private readonly IMemoryCache _cache;
    private readonly IUTorrentHttpService _httpService;
    private readonly DownloadClientConfig _config;
    private readonly ILogger<UTorrentAuthenticator> _logger;
    
    // Use a static concurrent dictionary to ensure same client instances share the same semaphore
    // This prevents multiple instances of the same client from authenticating simultaneously
    private readonly SemaphoreSlim _authSemaphore;
    private readonly string _clientKey;
    
    // Cache configuration - conservative timings to avoid token expiration issues
    private static readonly TimeSpan TokenExpiryDuration = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan CacheAbsoluteExpiration = TimeSpan.FromMinutes(25);

    public UTorrentAuthenticator(
        IMemoryCache cache,
        IUTorrentHttpService httpService,
        DownloadClientConfig config,
        ILogger<UTorrentAuthenticator> logger)
    {
        _cache = cache;
        _httpService = httpService;
        _config = config;
        _logger = logger;
        
        // Create unique client key based on connection details
        // This ensures different µTorrent instances don't share auth tokens
        _clientKey = GetClientKey();
        
        // Get or create semaphore for this specific client configuration
        if (_cache.TryGetValue<SemaphoreSlim>(_clientKey, out var authSemaphore) && authSemaphore is not null)
        {
            _authSemaphore = authSemaphore;
            return;
        }
        
        _authSemaphore = new SemaphoreSlim(1, 1);
        _cache.Set(_clientKey, _authSemaphore, Constants.DefaultCacheEntryOptions);
    }

    /// <inheritdoc/>
    public bool IsAuthenticated
    {
        get
        {
            var cacheKey = CacheKeys.UTorrent.GetAuthTokenKey(_clientKey);
            return _cache.TryGetValue(cacheKey, out UTorrentAuthCache? cachedAuth) && 
                   cachedAuth?.IsValid == true;
        }
    }

    /// <inheritdoc/>
    public string GuidCookie
    {
        get
        {
            var cacheKey = CacheKeys.UTorrent.GetAuthTokenKey(_clientKey);
            if (_cache.TryGetValue(cacheKey, out UTorrentAuthCache? cachedAuth) && 
                cachedAuth?.IsValid == true)
            {
                return cachedAuth.GuidCookie;
            }
            return string.Empty;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> EnsureAuthenticatedAsync()
    {
        var cacheKey = CacheKeys.UTorrent.GetAuthTokenKey(_clientKey);
        
        // Fast path: Check if we have valid cached auth
        if (_cache.TryGetValue(cacheKey, out UTorrentAuthCache? cachedAuth) && 
            cachedAuth?.IsValid == true)
        {
            return true;
        }
        
        // Slow path: Need to refresh authentication with concurrency control
        return await RefreshAuthenticationWithLockAsync();
    }

    /// <inheritdoc/>
    public async Task<string> GetValidTokenAsync()
    {
        if (!await EnsureAuthenticatedAsync())
        {
            throw new UTorrentAuthenticationException($"Failed to authenticate with µTorrent client '{_config.Name}'");
        }

        var cacheKey = CacheKeys.UTorrent.GetAuthTokenKey(_clientKey);
        if (_cache.TryGetValue(cacheKey, out UTorrentAuthCache? cachedAuth) && 
            cachedAuth?.IsValid == true)
        {
            return cachedAuth.AuthToken;
        }

        throw new UTorrentAuthenticationException($"Authentication token not available for µTorrent client '{_config.Name}'");
    }

    /// <inheritdoc/>
    public async Task<string> GetValidGuidCookieAsync()
    {
        if (!await EnsureAuthenticatedAsync())
        {
            throw new UTorrentAuthenticationException($"Failed to authenticate with µTorrent client '{_config.Name}'");
        }

        var cacheKey = CacheKeys.UTorrent.GetAuthTokenKey(_clientKey);
        if (_cache.TryGetValue(cacheKey, out UTorrentAuthCache? cachedAuth) && 
            cachedAuth?.IsValid == true)
        {
            return cachedAuth.GuidCookie;
        }

        throw new UTorrentAuthenticationException($"GUID cookie not available for µTorrent client '{_config.Name}'");
    }

    /// <inheritdoc/>
    public async Task RefreshSessionAsync()
    {
        const int maxRetries = 3;
        var retryCount = 0;
        var backoffDelay = TimeSpan.FromMilliseconds(500);
        
        while (retryCount < maxRetries)
        {
            try
            {
                var (token, guidCookie) = await _httpService.GetTokenAndCookieAsync();
                
                var authCache = new UTorrentAuthCache
                {
                    AuthToken = token,
                    GuidCookie = guidCookie,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.Add(TokenExpiryDuration)
                };
                
                // Cache with both sliding and absolute expiration
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheAbsoluteExpiration,
                    SlidingExpiration = TokenExpiryDuration,
                    Priority = CacheItemPriority.High
                };
                
                var cacheKey = CacheKeys.UTorrent.GetAuthTokenKey(_clientKey);
                _cache.Set(cacheKey, authCache, cacheOptions);
                
                return;
            }
            catch (Exception ex) when (retryCount < maxRetries - 1)
            {
                retryCount++;
                _logger.LogWarning(ex, "Authentication attempt {Attempt} failed for µTorrent client '{ClientName}', retrying in {Delay}ms", 
                    retryCount, _config.Name, backoffDelay.TotalMilliseconds);
                
                await Task.Delay(backoffDelay);
                backoffDelay = TimeSpan.FromMilliseconds(backoffDelay.TotalMilliseconds * 1.5); // Exponential backoff
            }
            catch (Exception ex)
            {
                // Invalidate any existing cache entry on failure
                await InvalidateSessionAsync();
                throw new UTorrentAuthenticationException($"Failed to refresh authentication session after {maxRetries} attempts: {ex.Message}", ex);
            }
        }
    }

    /// <inheritdoc/>
    public async Task InvalidateSessionAsync()
    {
        var cacheKey = CacheKeys.UTorrent.GetAuthTokenKey(_clientKey);
        _cache.Remove(cacheKey);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Refreshes authentication with concurrency control to prevent multiple simultaneous auth requests
    /// </summary>
    private async Task<bool> RefreshAuthenticationWithLockAsync()
    {
        var cacheKey = CacheKeys.UTorrent.GetAuthTokenKey(_clientKey);
        
        // Wait for our turn to authenticate (per client instance)
        await _authSemaphore.WaitAsync();
        
        try
        {
            // Double-check: another thread might have refreshed while we were waiting
            if (_cache.TryGetValue(cacheKey, out UTorrentAuthCache? cachedAuth) && 
                cachedAuth?.IsValid == true)
            {
                return true;
            }
            
            // Actually refresh the authentication
            await RefreshSessionAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh authentication for µTorrent client '{ClientName}'", _config.Name);
            return false;
        }
        finally
        {
            _authSemaphore.Release();
        }
    }

    /// <summary>
    /// Creates a unique client key based on connection details
    /// This ensures different µTorrent instances don't share auth tokens
    /// </summary>
    private string GetClientKey()
    {
        return _config.Url.ToString();
    }
}

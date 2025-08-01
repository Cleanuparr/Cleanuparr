using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.ContentBlocker;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Cleanuparr.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.ContentBlocker;

public sealed class BlocklistProvider
{
    private readonly ILogger<BlocklistProvider> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly Dictionary<InstanceType, string> _configHashes = new();
    private readonly Dictionary<string, DateTime> _lastLoadTimes = new();
    private const int DefaultLoadIntervalHours = 4;
    private const int FastLoadIntervalMinutes = 5;
    private const string MalwareListUrl = "https://cleanuparr.pages.dev/static/known_malware_file_name_patterns";
    private const string MalwareListKey = "MALWARE_PATTERNS";

    public BlocklistProvider(
        ILogger<BlocklistProvider> logger,
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory
    )
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _cache = cache;
        _httpClient = httpClientFactory.CreateClient(Constants.HttpClientWithRetryName);
    }

    public async Task LoadBlocklistsAsync()
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
            int changedCount = 0;
            var contentBlockerConfig = await dataContext.ContentBlockerConfigs
                .AsNoTracking()
                .FirstAsync();
            
            if (!contentBlockerConfig.Enabled)
            {
                _logger.LogDebug("Content blocker is disabled, skipping blocklist loading");
                return;
            }
            
            // Check and update Sonarr blocklist if needed
            string sonarrHash = GenerateSettingsHash(contentBlockerConfig.Sonarr);
            var sonarrInterval = GetLoadInterval(contentBlockerConfig.Sonarr.BlocklistPath);
            var sonarrIdentifier = $"Sonarr_{contentBlockerConfig.Sonarr.BlocklistPath}";
            if (ShouldReloadBlocklist(sonarrIdentifier, sonarrInterval) || !_configHashes.TryGetValue(InstanceType.Sonarr, out string? oldSonarrHash) || sonarrHash != oldSonarrHash)
            {
                _logger.LogDebug("Loading Sonarr blocklist");
                
                await LoadPatternsAndRegexesAsync(contentBlockerConfig.Sonarr, InstanceType.Sonarr);
                _configHashes[InstanceType.Sonarr] = sonarrHash;
                _lastLoadTimes[sonarrIdentifier] = DateTime.UtcNow;
                changedCount++;
            }
            
            // Check and update Radarr blocklist if needed
            string radarrHash = GenerateSettingsHash(contentBlockerConfig.Radarr);
            var radarrInterval = GetLoadInterval(contentBlockerConfig.Radarr.BlocklistPath);
            var radarrIdentifier = $"Radarr_{contentBlockerConfig.Radarr.BlocklistPath}";
            if (ShouldReloadBlocklist(radarrIdentifier, radarrInterval) || !_configHashes.TryGetValue(InstanceType.Radarr, out string? oldRadarrHash) || radarrHash != oldRadarrHash)
            {
                _logger.LogDebug("Loading Radarr blocklist");
                
                await LoadPatternsAndRegexesAsync(contentBlockerConfig.Radarr, InstanceType.Radarr);
                _configHashes[InstanceType.Radarr] = radarrHash;
                _lastLoadTimes[radarrIdentifier] = DateTime.UtcNow;
                changedCount++;
            }
            
            // Check and update Lidarr blocklist if needed
            string lidarrHash = GenerateSettingsHash(contentBlockerConfig.Lidarr);
            var lidarrInterval = GetLoadInterval(contentBlockerConfig.Lidarr.BlocklistPath);
            var lidarrIdentifier = $"Lidarr_{contentBlockerConfig.Lidarr.BlocklistPath}";
            if (ShouldReloadBlocklist(lidarrIdentifier, lidarrInterval) || !_configHashes.TryGetValue(InstanceType.Lidarr, out string? oldLidarrHash) || lidarrHash != oldLidarrHash)
            {
                _logger.LogDebug("Loading Lidarr blocklist");
                
                await LoadPatternsAndRegexesAsync(contentBlockerConfig.Lidarr, InstanceType.Lidarr);
                _configHashes[InstanceType.Lidarr] = lidarrHash;
                _lastLoadTimes[lidarrIdentifier] = DateTime.UtcNow;
                changedCount++;
            }
            
            // Check and update Readarr blocklist if needed
            string readarrHash = GenerateSettingsHash(contentBlockerConfig.Readarr);
            var readarrInterval = GetLoadInterval(contentBlockerConfig.Readarr.BlocklistPath);
            var readarrIdentifier = $"Readarr_{contentBlockerConfig.Readarr.BlocklistPath}";
            if (ShouldReloadBlocklist(readarrIdentifier, readarrInterval) || !_configHashes.TryGetValue(InstanceType.Readarr, out string? oldReadarrHash) || readarrHash != oldReadarrHash)
            {
                _logger.LogDebug("Loading Readarr blocklist");
                
                await LoadPatternsAndRegexesAsync(contentBlockerConfig.Readarr, InstanceType.Readarr);
                _configHashes[InstanceType.Readarr] = readarrHash;
                _lastLoadTimes[readarrIdentifier] = DateTime.UtcNow;
                changedCount++;
            }
            
            // Check and update Whisparr blocklist if needed
            string whisparrHash = GenerateSettingsHash(contentBlockerConfig.Whisparr);
            var whisparrInterval = GetLoadInterval(contentBlockerConfig.Whisparr.BlocklistPath);
            var whisparrIdentifier = $"Whisparr_{contentBlockerConfig.Whisparr.BlocklistPath}";
            if (ShouldReloadBlocklist(whisparrIdentifier, whisparrInterval) || !_configHashes.TryGetValue(InstanceType.Whisparr, out string? oldWhisparrHash) || whisparrHash != oldWhisparrHash)
            {
                _logger.LogDebug("Loading Whisparr blocklist");
                
                await LoadPatternsAndRegexesAsync(contentBlockerConfig.Whisparr, InstanceType.Whisparr);
                _configHashes[InstanceType.Whisparr] = whisparrHash;
                _lastLoadTimes[whisparrIdentifier] = DateTime.UtcNow;
                changedCount++;
            }
            
            // Always check and update malware patterns
            await LoadMalwarePatternsAsync();
            
            if (changedCount > 0)
            {
                _logger.LogInformation("Successfully loaded {count} blocklists", changedCount);
            }
            else
            {
                _logger.LogTrace("All blocklists are already up to date");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load blocklists");
            throw;
        }
    }

    public BlocklistType GetBlocklistType(InstanceType instanceType)
    {
        _cache.TryGetValue(CacheKeys.BlocklistType(instanceType), out BlocklistType? blocklistType);

        return blocklistType ?? BlocklistType.Blacklist;
    }
    
    public ConcurrentBag<string> GetPatterns(InstanceType instanceType)
    {
        _cache.TryGetValue(CacheKeys.BlocklistPatterns(instanceType), out ConcurrentBag<string>? patterns);

        return patterns ?? [];
    }

    public ConcurrentBag<Regex> GetRegexes(InstanceType instanceType)
    {
        _cache.TryGetValue(CacheKeys.BlocklistRegexes(instanceType), out ConcurrentBag<Regex>? regexes);
        
        return regexes ?? [];
    }
    
    public ConcurrentBag<string> GetMalwarePatterns()
    {
        _cache.TryGetValue(CacheKeys.KnownMalwarePatterns(), out ConcurrentBag<string>? patterns);
        
        return patterns ?? [];
    }
    
    private TimeSpan GetLoadInterval(string? path)
    {
        if (!string.IsNullOrEmpty(path) && Uri.TryCreate(path, UriKind.Absolute, out var uri))
        {
            if (uri.Host.Equals("cleanuparr.pages.dev", StringComparison.OrdinalIgnoreCase))
            {
                return TimeSpan.FromMinutes(FastLoadIntervalMinutes);
            }

            return TimeSpan.FromHours(DefaultLoadIntervalHours);
        }
        
        // If fast load interval for local files
        return TimeSpan.FromMinutes(FastLoadIntervalMinutes);
    }
    
    private bool ShouldReloadBlocklist(string identifier, TimeSpan interval)
    {
        if (!_lastLoadTimes.TryGetValue(identifier, out DateTime lastLoad))
        {
            return true;
        }
        
        return DateTime.UtcNow - lastLoad >= interval;
    }
    
    private async Task LoadMalwarePatternsAsync()
    {
        var malwareInterval = TimeSpan.FromMinutes(FastLoadIntervalMinutes);
        
        if (!ShouldReloadBlocklist(MalwareListKey, malwareInterval))
        {
            return;
        }
        
        try
        {
            _logger.LogDebug("Loading malware patterns");
            
            string[] filePatterns = await ReadContentAsync(MalwareListUrl);
            
            long startTime = Stopwatch.GetTimestamp();
            ParallelOptions options = new() { MaxDegreeOfParallelism = 5 };
            ConcurrentBag<string> patterns = [];
            
            Parallel.ForEach(filePatterns, options, pattern =>
            {
                patterns.Add(pattern);
            });

            TimeSpan elapsed = Stopwatch.GetElapsedTime(startTime);

            _cache.Set(CacheKeys.KnownMalwarePatterns(), patterns);
            _lastLoadTimes[MalwareListKey] = DateTime.UtcNow;
            
            _logger.LogDebug("loaded {count} known malware patterns", patterns.Count);
            _logger.LogDebug("malware patterns loaded in {elapsed} ms", elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load malware patterns from {url}", MalwareListUrl);
        }
    }

    private async Task LoadPatternsAndRegexesAsync(BlocklistSettings blocklistSettings, InstanceType instanceType)
    {
        if (string.IsNullOrEmpty(blocklistSettings.BlocklistPath))
        {
            return;
        }
        
        string[] filePatterns = await ReadContentAsync(blocklistSettings.BlocklistPath);
        
        long startTime = Stopwatch.GetTimestamp();
        ParallelOptions options = new() { MaxDegreeOfParallelism = 5 };
        const string regexId = "regex:";
        ConcurrentBag<string> patterns = [];
        ConcurrentBag<Regex> regexes = [];
        
        Parallel.ForEach(filePatterns, options, pattern =>
        {
            if (!pattern.StartsWith(regexId))
            {
                patterns.Add(pattern);
                return;
            }
            
            pattern = pattern[regexId.Length..];
            
            try
            {
                Regex regex = new(pattern, RegexOptions.Compiled);
                regexes.Add(regex);
            }
            catch (ArgumentException)
            {
                _logger.LogWarning("invalid regex | {pattern}", pattern);
            }
        });

        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTime);

        _cache.Set(CacheKeys.BlocklistType(instanceType), blocklistSettings.BlocklistType);
        _cache.Set(CacheKeys.BlocklistPatterns(instanceType), patterns);
        _cache.Set(CacheKeys.BlocklistRegexes(instanceType), regexes);
        
        _logger.LogDebug("loaded {count} patterns", patterns.Count);
        _logger.LogDebug("loaded {count} regexes", regexes.Count);
        _logger.LogDebug("blocklist loaded in {elapsed} ms | {path}", elapsed.TotalMilliseconds, blocklistSettings.BlocklistPath);
    }
    
    private async Task<string[]> ReadContentAsync(string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            // http(s) url
            return await ReadFromUrlAsync(path);
        }

        if (File.Exists(path))
        {
            // local file path
            return await File.ReadAllLinesAsync(path);
        }

        throw new ArgumentException($"blocklist not found | {path}");
    }

    private async Task<string[]> ReadFromUrlAsync(string url)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        
        return (await response.Content.ReadAsStringAsync())
            .Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries);
    }
    
    private string GenerateSettingsHash(BlocklistSettings blocklistSettings)
    {
        // Create a string that represents the relevant blocklist configuration
        var configStr = $"{blocklistSettings.BlocklistPath ?? string.Empty}|{blocklistSettings.BlocklistType}";
        
        // Create SHA256 hash of the configuration string
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(configStr);
        var hashBytes = sha.ComputeHash(bytes);
        
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
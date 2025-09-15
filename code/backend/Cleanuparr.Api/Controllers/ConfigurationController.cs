using Cleanuparr.Api.Models;
using Cleanuparr.Api.Models.NotificationProviders;
using Cleanuparr.Application.Features.Arr.Dtos;
using Cleanuparr.Application.Features.DownloadClient.Dtos;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Cleanuparr.Infrastructure.Logging;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Infrastructure.Utilities;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Cleanuparr.Persistence.Models.Configuration.BlacklistSync;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cleanuparr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : ControllerBase
{
    private readonly ILogger<ConfigurationController> _logger;
    private readonly DataContext _dataContext;
    private readonly IJobManagementService _jobManagementService;
    private readonly MemoryCache _cache;
    private readonly INotificationConfigurationService _notificationConfigurationService;
    private readonly NotificationService _notificationService;

    public ConfigurationController(
        ILogger<ConfigurationController> logger,
        DataContext dataContext,
        IJobManagementService jobManagementService,
        MemoryCache cache,
        INotificationConfigurationService notificationConfigurationService,
        NotificationService notificationService
    )
    {
        _logger = logger;
        _dataContext = dataContext;
        _jobManagementService = jobManagementService;
        _cache = cache;
        _notificationConfigurationService = notificationConfigurationService;
        _notificationService = notificationService;
    }

    [HttpGet("blacklist_sync")]
    public async Task<IActionResult> GetBlacklistSyncConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.BlacklistSyncConfigs
                .AsNoTracking()
                .FirstAsync();
            return Ok(config);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpGet("queue_cleaner")]
    public async Task<IActionResult> GetQueueCleanerConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.QueueCleanerConfigs
                .AsNoTracking()
                .FirstAsync();
            return Ok(config);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
    
    [HttpGet("malware_blocker")]
    public async Task<IActionResult> GetMalwareBlockerConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.ContentBlockerConfigs
                .AsNoTracking()
                .FirstAsync();
            return Ok(config);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpGet("download_cleaner")]
    public async Task<IActionResult> GetDownloadCleanerConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.DownloadCleanerConfigs
                .Include(x => x.Categories)
                .AsNoTracking()
                .FirstAsync();
            return Ok(config);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpGet("download_client")]
    public async Task<IActionResult> GetDownloadClientConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var clients = await _dataContext.DownloadClients
                .AsNoTracking()
                .ToListAsync();
            
            // Return in the expected format with clients wrapper
            var config = new { clients = clients };
            return Ok(config);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
    
    [HttpPost("download_client")]
    public async Task<IActionResult> CreateDownloadClientConfig([FromBody] CreateDownloadClientDto newClient)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Validate the configuration
            newClient.Validate();
            
            // Create the full config from the DTO
            var clientConfig = new DownloadClientConfig
            {
                Enabled = newClient.Enabled,
                Name = newClient.Name,
                TypeName = newClient.TypeName,
                Type = newClient.Type,
                Host = newClient.Host,
                Username = newClient.Username,
                Password = newClient.Password,
                UrlBase = newClient.UrlBase
            };
            
            // Add the new client to the database
            _dataContext.DownloadClients.Add(clientConfig);
            await _dataContext.SaveChangesAsync();
            
            return CreatedAtAction(nameof(GetDownloadClientConfig), new { id = clientConfig.Id }, clientConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create download client");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
    
    [HttpPut("download_client/{id}")]
    public async Task<IActionResult> UpdateDownloadClientConfig(Guid id, [FromBody] DownloadClientConfig updatedClient)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Find the existing download client
            var existingClient = await _dataContext.DownloadClients
                .FirstOrDefaultAsync(c => c.Id == id);
                
            if (existingClient == null)
            {
                return NotFound($"Download client with ID {id} not found");
            }
            
            // Ensure the ID in the path matches the entity being updated
            updatedClient = updatedClient with { Id = id };
            
            // Apply updates from DTO
            updatedClient.Adapt(existingClient);
            
            // Persist the configuration
            await _dataContext.SaveChangesAsync();
            
            return Ok(existingClient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update download client with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
    
    [HttpDelete("download_client/{id}")]
    public async Task<IActionResult> DeleteDownloadClientConfig(Guid id)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Find the existing download client
            var existingClient = await _dataContext.DownloadClients
                .FirstOrDefaultAsync(c => c.Id == id);
                
            if (existingClient == null)
            {
                return NotFound($"Download client with ID {id} not found");
            }
            
            // Remove the client from the database
            _dataContext.DownloadClients.Remove(existingClient);
            await _dataContext.SaveChangesAsync();
            
            // Clean up any registered HTTP client configuration
            var dynamicHttpClientFactory = HttpContext.RequestServices
                .GetRequiredService<IDynamicHttpClientFactory>();
                
            var clientName = $"DownloadClient_{id}";
            dynamicHttpClientFactory.UnregisterConfiguration(clientName);
            
            _logger.LogInformation("Removed HTTP client configuration for deleted download client {ClientName}", clientName);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete download client with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpGet("general")]
    public async Task<IActionResult> GetGeneralConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.GeneralConfigs
                .AsNoTracking()
                .FirstAsync();
            return Ok(config);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpGet("sonarr")]
    public async Task<IActionResult> GetSonarrConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.ArrConfigs
                .Include(x => x.Instances)
                .AsNoTracking()
                .FirstAsync(x => x.Type == InstanceType.Sonarr);
            return Ok(config.Adapt<ArrConfigDto>());
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpGet("radarr")]
    public async Task<IActionResult> GetRadarrConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.ArrConfigs
                .Include(x => x.Instances)
                .AsNoTracking()
                .FirstAsync(x => x.Type == InstanceType.Radarr);
            return Ok(config.Adapt<ArrConfigDto>());
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpGet("lidarr")]
    public async Task<IActionResult> GetLidarrConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.ArrConfigs
                .Include(x => x.Instances)
                .AsNoTracking()
                .FirstAsync(x => x.Type == InstanceType.Lidarr);
            return Ok(config.Adapt<ArrConfigDto>());
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpGet("readarr")]
    public async Task<IActionResult> GetReadarrConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.ArrConfigs
                .Include(x => x.Instances)
                .AsNoTracking()
                .FirstAsync(x => x.Type == InstanceType.Readarr);
            return Ok(config.Adapt<ArrConfigDto>());
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpGet("whisparr")]
    public async Task<IActionResult> GetWhisparrConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.ArrConfigs
                .Include(x => x.Instances)
                .AsNoTracking()
                .FirstAsync(x => x.Type == InstanceType.Whisparr);
            return Ok(config.Adapt<ArrConfigDto>());
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpGet("notification_providers")]
    public async Task<IActionResult> GetNotificationProviders()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var providers = await _dataContext.NotificationConfigs
                .Include(p => p.NotifiarrConfiguration)
                .Include(p => p.AppriseConfiguration)
                .AsNoTracking()
                .ToListAsync();
            
            var providerDtos = providers
                .Select(p => new NotificationProviderDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Type = p.Type,
                    IsEnabled = p.IsEnabled,
                    Events = new NotificationEventFlags
                    {
                        OnFailedImportStrike = p.OnFailedImportStrike,
                        OnStalledStrike = p.OnStalledStrike,
                        OnSlowStrike = p.OnSlowStrike,
                        OnQueueItemDeleted = p.OnQueueItemDeleted,
                        OnDownloadCleaned = p.OnDownloadCleaned,
                        OnCategoryChanged = p.OnCategoryChanged
                    },
                    Configuration = p.Type switch
                    {
                        NotificationProviderType.Notifiarr => p.NotifiarrConfiguration ?? new object(),
                        NotificationProviderType.Apprise => p.AppriseConfiguration ?? new object(),
                        _ => new object()
                    }
                })
                .OrderBy(x => x.Type.ToString())
                .ThenBy(x => x.Name)
                .ToList();
            
            // Return in the expected format with providers wrapper
            var config = new { providers = providerDtos };
            return Ok(config);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
    
    [HttpPost("notification_providers/notifiarr")]
    public async Task<IActionResult> CreateNotifiarrProvider([FromBody] CreateNotifiarrProviderDto newProvider)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(newProvider.Name))
            {
                return BadRequest("Provider name is required");
            }
            
            var duplicateConfig = await _dataContext.NotificationConfigs.CountAsync(x => x.Name == newProvider.Name);

            if (duplicateConfig > 0)
            {
                return BadRequest("A provider with this name already exists");
            }

            // Create provider-specific configuration with validation
            var notifiarrConfig = new NotifiarrConfig
            {
                ApiKey = newProvider.ApiKey,
                ChannelId = newProvider.ChannelId
            };
            
            // Validate the configuration
            notifiarrConfig.Validate();

            // Create the provider entity
            var provider = new NotificationConfig
            {
                Name = newProvider.Name,
                Type = NotificationProviderType.Notifiarr,
                IsEnabled = newProvider.IsEnabled,
                OnFailedImportStrike = newProvider.OnFailedImportStrike,
                OnStalledStrike = newProvider.OnStalledStrike,
                OnSlowStrike = newProvider.OnSlowStrike,
                OnQueueItemDeleted = newProvider.OnQueueItemDeleted,
                OnDownloadCleaned = newProvider.OnDownloadCleaned,
                OnCategoryChanged = newProvider.OnCategoryChanged,
                NotifiarrConfiguration = notifiarrConfig
            };

            // Add the new provider to the database
            _dataContext.NotificationConfigs.Add(provider);
            await _dataContext.SaveChangesAsync();

            // Clear cache to ensure fresh data on next request
            await _notificationConfigurationService.InvalidateCacheAsync();

            // Return the provider in DTO format to match frontend expectations
            var providerDto = new NotificationProviderDto
            {
                Id = provider.Id,
                Name = provider.Name,
                Type = provider.Type,
                IsEnabled = provider.IsEnabled,
                Events = new NotificationEventFlags
                {
                    OnFailedImportStrike = provider.OnFailedImportStrike,
                    OnStalledStrike = provider.OnStalledStrike,
                    OnSlowStrike = provider.OnSlowStrike,
                    OnQueueItemDeleted = provider.OnQueueItemDeleted,
                    OnDownloadCleaned = provider.OnDownloadCleaned,
                    OnCategoryChanged = provider.OnCategoryChanged
                },
                Configuration = provider.NotifiarrConfiguration ?? new object()
            };

            return CreatedAtAction(nameof(GetNotificationProviders), new { id = provider.Id }, providerDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Notifiarr provider");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPost("notification_providers/apprise")]
    public async Task<IActionResult> CreateAppriseProvider([FromBody] CreateAppriseProviderDto newProvider)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(newProvider.Name))
            {
                return BadRequest("Provider name is required");
            }
            
            var duplicateConfig = await _dataContext.NotificationConfigs.CountAsync(x => x.Name == newProvider.Name);

            if (duplicateConfig > 0)
            {
                return BadRequest("A provider with this name already exists");
            }

            // Create provider-specific configuration with validation
            var appriseConfig = new AppriseConfig
            {
                Url = newProvider.Url,
                Key = newProvider.Key,
                Tags = newProvider.Tags
            };
            
            // Validate the configuration
            appriseConfig.Validate();

            // Create the provider entity
            var provider = new NotificationConfig
            {
                Name = newProvider.Name,
                Type = NotificationProviderType.Apprise,
                IsEnabled = newProvider.IsEnabled,
                OnFailedImportStrike = newProvider.OnFailedImportStrike,
                OnStalledStrike = newProvider.OnStalledStrike,
                OnSlowStrike = newProvider.OnSlowStrike,
                OnQueueItemDeleted = newProvider.OnQueueItemDeleted,
                OnDownloadCleaned = newProvider.OnDownloadCleaned,
                OnCategoryChanged = newProvider.OnCategoryChanged,
                AppriseConfiguration = appriseConfig
            };

            // Add the new provider to the database
            _dataContext.NotificationConfigs.Add(provider);
            await _dataContext.SaveChangesAsync();

            // Clear cache to ensure fresh data on next request
            await _notificationConfigurationService.InvalidateCacheAsync();

            // Return the provider in DTO format to match frontend expectations
            var providerDto = new NotificationProviderDto
            {
                Id = provider.Id,
                Name = provider.Name,
                Type = provider.Type,
                IsEnabled = provider.IsEnabled,
                Events = new NotificationEventFlags
                {
                    OnFailedImportStrike = provider.OnFailedImportStrike,
                    OnStalledStrike = provider.OnStalledStrike,
                    OnSlowStrike = provider.OnSlowStrike,
                    OnQueueItemDeleted = provider.OnQueueItemDeleted,
                    OnDownloadCleaned = provider.OnDownloadCleaned,
                    OnCategoryChanged = provider.OnCategoryChanged
                },
                Configuration = provider.AppriseConfiguration ?? new object()
            };

            return CreatedAtAction(nameof(GetNotificationProviders), new { id = provider.Id }, providerDto);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Apprise provider");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
    
    // Provider-specific UPDATE endpoints
    [HttpPut("notification_providers/notifiarr/{id}")]
    public async Task<IActionResult> UpdateNotifiarrProvider(Guid id, [FromBody] UpdateNotifiarrProviderDto updatedProvider)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Find the existing notification provider
            var existingProvider = await _dataContext.NotificationConfigs
                .Include(p => p.NotifiarrConfiguration)
                .FirstOrDefaultAsync(p => p.Id == id && p.Type == NotificationProviderType.Notifiarr);
                
            if (existingProvider == null)
            {
                return NotFound($"Notifiarr provider with ID {id} not found");
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(updatedProvider.Name))
            {
                return BadRequest("Provider name is required");
            }

            var duplicateConfig = await _dataContext.NotificationConfigs
                .Where(x => x.Id != id)
                .Where(x => x.Name == updatedProvider.Name)
                .CountAsync();

            if (duplicateConfig > 0)
            {
                return BadRequest("A provider with this name already exists");
            }
            
            // Create provider-specific configuration with validation
            var notifiarrConfig = new NotifiarrConfig
            {
                ApiKey = updatedProvider.ApiKey,
                ChannelId = updatedProvider.ChannelId
            };
            
            // Preserve the existing ID if updating
            if (existingProvider.NotifiarrConfiguration != null)
            {
                notifiarrConfig = notifiarrConfig with { Id = existingProvider.NotifiarrConfiguration.Id };
            }
            
            // Validate the configuration
            notifiarrConfig.Validate();

            // Create a new provider entity with updated values (records are immutable)
            var newProvider = existingProvider with
            {
                Name = updatedProvider.Name,
                IsEnabled = updatedProvider.IsEnabled,
                OnFailedImportStrike = updatedProvider.OnFailedImportStrike,
                OnStalledStrike = updatedProvider.OnStalledStrike,
                OnSlowStrike = updatedProvider.OnSlowStrike,
                OnQueueItemDeleted = updatedProvider.OnQueueItemDeleted,
                OnDownloadCleaned = updatedProvider.OnDownloadCleaned,
                OnCategoryChanged = updatedProvider.OnCategoryChanged,
                NotifiarrConfiguration = notifiarrConfig,
                UpdatedAt = DateTime.UtcNow
            };

            // Remove old and add new (EF handles this as an update)
            _dataContext.NotificationConfigs.Remove(existingProvider);
            _dataContext.NotificationConfigs.Add(newProvider);
            
            // Persist the configuration
            await _dataContext.SaveChangesAsync();

            // Clear cache to ensure fresh data on next request
            await _notificationConfigurationService.InvalidateCacheAsync();

            // Return the provider in DTO format to match frontend expectations
            var providerDto = new NotificationProviderDto
            {
                Id = newProvider.Id,
                Name = newProvider.Name,
                Type = newProvider.Type,
                IsEnabled = newProvider.IsEnabled,
                Events = new NotificationEventFlags
                {
                    OnFailedImportStrike = newProvider.OnFailedImportStrike,
                    OnStalledStrike = newProvider.OnStalledStrike,
                    OnSlowStrike = newProvider.OnSlowStrike,
                    OnQueueItemDeleted = newProvider.OnQueueItemDeleted,
                    OnDownloadCleaned = newProvider.OnDownloadCleaned,
                    OnCategoryChanged = newProvider.OnCategoryChanged
                },
                Configuration = newProvider.NotifiarrConfiguration ?? new object()
            };

            return Ok(providerDto);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Notifiarr provider with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
    
    [HttpPut("notification_providers/apprise/{id}")]
    public async Task<IActionResult> UpdateAppriseProvider(Guid id, [FromBody] UpdateAppriseProviderDto updatedProvider)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Find the existing notification provider
            var existingProvider = await _dataContext.NotificationConfigs
                .Include(p => p.AppriseConfiguration)
                .FirstOrDefaultAsync(p => p.Id == id && p.Type == NotificationProviderType.Apprise);
                
            if (existingProvider == null)
            {
                return NotFound($"Apprise provider with ID {id} not found");
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(updatedProvider.Name))
            {
                return BadRequest("Provider name is required");
            }
            
            var duplicateConfig = await _dataContext.NotificationConfigs
                .Where(x => x.Id != id)
                .Where(x => x.Name == updatedProvider.Name)
                .CountAsync();

            if (duplicateConfig > 0)
            {
                return BadRequest("A provider with this name already exists");
            }

            // Create provider-specific configuration with validation
            var appriseConfig = new AppriseConfig
            {
                Url = updatedProvider.Url,
                Key = updatedProvider.Key,
                Tags = updatedProvider.Tags
            };
            
            // Preserve the existing ID if updating
            if (existingProvider.AppriseConfiguration != null)
            {
                appriseConfig = appriseConfig with { Id = existingProvider.AppriseConfiguration.Id };
            }
            
            // Validate the configuration
            appriseConfig.Validate();

            // Create a new provider entity with updated values (records are immutable)
            var newProvider = existingProvider with
            {
                Name = updatedProvider.Name,
                IsEnabled = updatedProvider.IsEnabled,
                OnFailedImportStrike = updatedProvider.OnFailedImportStrike,
                OnStalledStrike = updatedProvider.OnStalledStrike,
                OnSlowStrike = updatedProvider.OnSlowStrike,
                OnQueueItemDeleted = updatedProvider.OnQueueItemDeleted,
                OnDownloadCleaned = updatedProvider.OnDownloadCleaned,
                OnCategoryChanged = updatedProvider.OnCategoryChanged,
                AppriseConfiguration = appriseConfig,
                UpdatedAt = DateTime.UtcNow
            };

            // Remove old and add new (EF handles this as an update)
            _dataContext.NotificationConfigs.Remove(existingProvider);
            _dataContext.NotificationConfigs.Add(newProvider);
            
            // Persist the configuration
            await _dataContext.SaveChangesAsync();

            // Clear cache to ensure fresh data on next request
            await _notificationConfigurationService.InvalidateCacheAsync();

            // Return the provider in DTO format to match frontend expectations
            var providerDto = new NotificationProviderDto
            {
                Id = newProvider.Id,
                Name = newProvider.Name,
                Type = newProvider.Type,
                IsEnabled = newProvider.IsEnabled,
                Events = new NotificationEventFlags
                {
                    OnFailedImportStrike = newProvider.OnFailedImportStrike,
                    OnStalledStrike = newProvider.OnStalledStrike,
                    OnSlowStrike = newProvider.OnSlowStrike,
                    OnQueueItemDeleted = newProvider.OnQueueItemDeleted,
                    OnDownloadCleaned = newProvider.OnDownloadCleaned,
                    OnCategoryChanged = newProvider.OnCategoryChanged
                },
                Configuration = newProvider.AppriseConfiguration ?? new object()
            };

            return Ok(providerDto);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Apprise provider with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
    
    [HttpDelete("notification_providers/{id}")]
    public async Task<IActionResult> DeleteNotificationProvider(Guid id)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Find the existing notification provider
            var existingProvider = await _dataContext.NotificationConfigs
                .Include(p => p.NotifiarrConfiguration)
                .Include(p => p.AppriseConfiguration)
                .FirstOrDefaultAsync(p => p.Id == id);
                
            if (existingProvider == null)
            {
                return NotFound($"Notification provider with ID {id} not found");
            }
            
            // Remove the provider from the database
            _dataContext.NotificationConfigs.Remove(existingProvider);
            await _dataContext.SaveChangesAsync();

            // Clear cache to ensure fresh data on next request
            await _notificationConfigurationService.InvalidateCacheAsync();
            
            _logger.LogInformation("Removed notification provider {ProviderName} with ID {ProviderId}", 
                existingProvider.Name, existingProvider.Id);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete notification provider with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    // Provider-specific TEST endpoints (no ID required)
    [HttpPost("notification_providers/notifiarr/test")]
    public async Task<IActionResult> TestNotifiarrProvider([FromBody] TestNotifiarrProviderDto testRequest)
    {
        try
        {
            // Create configuration for testing with validation
            var notifiarrConfig = new NotifiarrConfig
            {
                ApiKey = testRequest.ApiKey,
                ChannelId = testRequest.ChannelId
            };
            
            // Validate the configuration
            notifiarrConfig.Validate();

            // Create a temporary provider DTO for the test service
            var providerDto = new NotificationProviderDto
            {
                Id = Guid.NewGuid(), // Temporary ID for testing
                Name = "Test Provider",
                Type = NotificationProviderType.Notifiarr,
                IsEnabled = true,
                Events = new NotificationEventFlags
                {
                    OnFailedImportStrike = true, // Enable for test
                    OnStalledStrike = false,
                    OnSlowStrike = false,
                    OnQueueItemDeleted = false,
                    OnDownloadCleaned = false,
                    OnCategoryChanged = false
                },
                Configuration = notifiarrConfig
            };

            // Test the notification provider
            await _notificationService.SendTestNotificationAsync(providerDto);
            
            return Ok(new { Message = "Test notification sent successfully", Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test Notifiarr provider");
            throw;
        }
    }

    [HttpPost("notification_providers/apprise/test")]
    public async Task<IActionResult> TestAppriseProvider([FromBody] TestAppriseProviderDto testRequest)
    {
        try
        {
            // Create configuration for testing with validation
            var appriseConfig = new AppriseConfig
            {
                Url = testRequest.Url,
                Key = testRequest.Key,
                Tags = testRequest.Tags
            };
            
            // Validate the configuration
            appriseConfig.Validate();

            // Create a temporary provider DTO for the test service
            var providerDto = new NotificationProviderDto
            {
                Id = Guid.NewGuid(), // Temporary ID for testing
                Name = "Test Provider",
                Type = NotificationProviderType.Apprise,
                IsEnabled = true,
                Events = new NotificationEventFlags
                {
                    OnFailedImportStrike = true, // Enable for test
                    OnStalledStrike = false,
                    OnSlowStrike = false,
                    OnQueueItemDeleted = false,
                    OnDownloadCleaned = false,
                    OnCategoryChanged = false
                },
                Configuration = appriseConfig
            };

            // Test the notification provider
            await _notificationService.SendTestNotificationAsync(providerDto);
            
            return Ok(new { Message = "Test notification sent successfully", Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test Apprise provider");
            throw;
        }
    }

    [HttpPut("queue_cleaner")]
    public async Task<IActionResult> UpdateQueueCleanerConfig([FromBody] QueueCleanerConfig newConfig)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Validate the configuration
            newConfig.Validate();

            // Validate cron expression if present
            if (!string.IsNullOrEmpty(newConfig.CronExpression))
            {
                CronValidationHelper.ValidateCronExpression(newConfig.CronExpression);
            }

            // Get existing config
            var oldConfig = await _dataContext.QueueCleanerConfigs
                .FirstAsync();

            // Apply updates from DTO, excluding the ID property to avoid EF key modification error
            var adapterConfig = new TypeAdapterConfig();
            adapterConfig.NewConfig<QueueCleanerConfig, QueueCleanerConfig>()
                .Ignore(dest => dest.Id);
            
            newConfig.Adapt(oldConfig, adapterConfig);

            // Persist the configuration
            await _dataContext.SaveChangesAsync();

            // Update the scheduler based on configuration changes
            await UpdateJobSchedule(oldConfig, JobType.QueueCleaner);

            return Ok(new { Message = "QueueCleaner configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save QueueCleaner configuration");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
    
    [HttpPut("malware_blocker")]
    public async Task<IActionResult> UpdateMalwareBlockerConfig([FromBody] ContentBlockerConfig newConfig)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Validate the configuration
            newConfig.Validate();

            // Validate cron expression if present
            if (!string.IsNullOrEmpty(newConfig.CronExpression))
            {
                CronValidationHelper.ValidateCronExpression(newConfig.CronExpression, JobType.MalwareBlocker);
            }

            // Get existing config
            var oldConfig = await _dataContext.ContentBlockerConfigs
                .FirstAsync();

            // Apply updates from DTO, excluding the ID property to avoid EF key modification error
            var config = new TypeAdapterConfig();
            config.NewConfig<ContentBlockerConfig, ContentBlockerConfig>()
                .Ignore(dest => dest.Id);
            
            newConfig.Adapt(oldConfig, config);

            // Persist the configuration
            await _dataContext.SaveChangesAsync();

            // Update the scheduler based on configuration changes
            await UpdateJobSchedule(oldConfig, JobType.MalwareBlocker);

            return Ok(new { Message = "MalwareBlocker configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save MalwareBlocker configuration");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("download_cleaner")]
    public async Task<IActionResult> UpdateDownloadCleanerConfig([FromBody] UpdateDownloadCleanerConfigDto newConfigDto)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Validate cron expression if present
            if (!string.IsNullOrEmpty(newConfigDto.CronExpression))
            {
                CronValidationHelper.ValidateCronExpression(newConfigDto.CronExpression);
            }
            
            // Validate categories
            if (newConfigDto.Enabled && newConfigDto.Categories.Any())
            {
                // Check for duplicate category names
                if (newConfigDto.Categories.GroupBy(x => x.Name).Any(x => x.Count() > 1))
                {
                    throw new ValidationException("Duplicate category names found");
                }
                
                // Validate each category
                foreach (var categoryDto in newConfigDto.Categories)
                {
                    if (string.IsNullOrEmpty(categoryDto.Name.Trim()))
                    {
                        throw new ValidationException("Category name cannot be empty");
                    }
                    
                    if (categoryDto is { MaxRatio: < 0, MaxSeedTime: < 0 })
                    {
                        throw new ValidationException("Either max ratio or max seed time must be enabled");
                    }
                    
                    if (categoryDto.MinSeedTime < 0)
                    {
                        throw new ValidationException("Min seed time cannot be negative");
                    }
                }
            }
            
            // Validate unlinked settings if enabled
            if (newConfigDto.UnlinkedEnabled)
            {
                if (string.IsNullOrEmpty(newConfigDto.UnlinkedTargetCategory))
                {
                    throw new ValidationException("Unlinked target category cannot be empty");
                }

                if (newConfigDto.UnlinkedCategories?.Count is null or 0)
                {
                    throw new ValidationException("Unlinked categories cannot be empty");
                }

                if (newConfigDto.UnlinkedCategories.Contains(newConfigDto.UnlinkedTargetCategory))
                {
                    throw new ValidationException("The unlinked target category should not be present in unlinked categories");
                }

                if (newConfigDto.UnlinkedCategories.Any(string.IsNullOrEmpty))
                {
                    throw new ValidationException("Empty unlinked category filter found");
                }

                if (!string.IsNullOrEmpty(newConfigDto.UnlinkedIgnoredRootDir) && !Directory.Exists(newConfigDto.UnlinkedIgnoredRootDir))
                {
                    throw new ValidationException($"{newConfigDto.UnlinkedIgnoredRootDir} root directory does not exist");
                }
            }

            // Get existing config
            var oldConfig = await _dataContext.DownloadCleanerConfigs
                .Include(x => x.Categories)
                .FirstAsync();

            // Update the main properties from DTO

            oldConfig.Enabled = newConfigDto.Enabled;
            oldConfig.CronExpression = newConfigDto.CronExpression;
            oldConfig.UseAdvancedScheduling = newConfigDto.UseAdvancedScheduling;
            oldConfig.DeletePrivate = newConfigDto.DeletePrivate;
            oldConfig.UnlinkedEnabled = newConfigDto.UnlinkedEnabled;
            oldConfig.UnlinkedTargetCategory = newConfigDto.UnlinkedTargetCategory;
            oldConfig.UnlinkedUseTag = newConfigDto.UnlinkedUseTag;
            oldConfig.UnlinkedIgnoredRootDir = newConfigDto.UnlinkedIgnoredRootDir;
            oldConfig.UnlinkedCategories = newConfigDto.UnlinkedCategories;
            oldConfig.IgnoredDownloads = newConfigDto.IgnoredDownloads;

            // Handle Categories collection separately to avoid EF tracking issues
            // Clear existing categories
            _dataContext.CleanCategories.RemoveRange(oldConfig.Categories);
            _dataContext.DownloadCleanerConfigs.Update(oldConfig);
            
            // Add new categories
            foreach (var categoryDto in newConfigDto.Categories)
            {
                _dataContext.CleanCategories.Add(new CleanCategory
                {
                    Name = categoryDto.Name,
                    MaxRatio = categoryDto.MaxRatio,
                    MinSeedTime = categoryDto.MinSeedTime,
                    MaxSeedTime = categoryDto.MaxSeedTime,
                    DownloadCleanerConfigId = oldConfig.Id
                });
            }

            // Persist the configuration
            await _dataContext.SaveChangesAsync();

            // Update the scheduler based on configuration changes
            await UpdateJobSchedule(oldConfig, JobType.DownloadCleaner);

            return Ok(new { Message = "DownloadCleaner configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save DownloadCleaner configuration");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
    
    [HttpPut("general")]
    public async Task<IActionResult> UpdateGeneralConfig([FromBody] GeneralConfig newConfig)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Validate the configuration
            newConfig.Validate();

            // Get existing config
            var oldConfig = await _dataContext.GeneralConfigs
                .FirstAsync();

            // Apply updates from DTO, excluding the ID property to avoid EF key modification error
            var config = new TypeAdapterConfig();
            config.NewConfig<GeneralConfig, GeneralConfig>()
                .Ignore(dest => dest.Id);

            if (oldConfig.DryRun && !newConfig.DryRun)
            {
                foreach (string strikeType in Enum.GetNames(typeof(StrikeType)))
                {
                    var keys = _cache.Keys
                        .Where(key => key.ToString()?.StartsWith(strikeType, StringComparison.InvariantCultureIgnoreCase) is true)
                        .ToList();

                    foreach (object key in keys)
                    {
                        _cache.Remove(key);
                    }
                    
                    _logger.LogTrace("Removed all cache entries for strike type: {StrikeType}", strikeType);
                }
            }
            
            // Handle logging configuration changes
            var loggingChanged = HasLoggingConfigurationChanged(oldConfig.Log, newConfig.Log);
            
            newConfig.Adapt(oldConfig, config);

            // Persist the configuration
            await _dataContext.SaveChangesAsync();

            // Update all HTTP client configurations with new general settings
            var dynamicHttpClientFactory = HttpContext.RequestServices
                .GetRequiredService<IDynamicHttpClientFactory>();
            
            dynamicHttpClientFactory.UpdateAllClientsFromGeneralConfig(oldConfig);
            
            _logger.LogInformation("Updated all HTTP client configurations with new general settings");
            
            if (loggingChanged.LevelOnly)
            {
                _logger.LogCritical("Setting global log level to {level}", newConfig.Log.Level);
                LoggingConfigManager.SetLogLevel(newConfig.Log.Level);
            }
            else if (loggingChanged.FullReconfiguration)
            {
                _logger.LogCritical("Reconfiguring logger due to configuration changes");
                LoggingConfigManager.ReconfigureLogging(newConfig);
            }

            return Ok(new { Message = "General configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save General configuration");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("blacklist_sync")]
    public async Task<IActionResult> UpdateBlacklistSyncConfig([FromBody] BlacklistSyncConfig newConfig)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            newConfig.Validate();

            var oldConfig = await _dataContext.BlacklistSyncConfigs
                .FirstAsync();

            bool enabledChanged = oldConfig.Enabled != newConfig.Enabled;
            bool becameEnabled = !oldConfig.Enabled && newConfig.Enabled;
            bool pathChanged = !(oldConfig.BlacklistPath?.Equals(newConfig.BlacklistPath, StringComparison.InvariantCultureIgnoreCase) ?? true);

            var adapterConfig = new TypeAdapterConfig();
            adapterConfig.NewConfig<BlacklistSyncConfig, BlacklistSyncConfig>()
                .Ignore(dest => dest.Id)
                // Cron expression changes are not supported yet for this type of job
                .Ignore(dest => dest.CronExpression);

            newConfig.Adapt(oldConfig, adapterConfig);

            await _dataContext.SaveChangesAsync();

            if (enabledChanged)
            {
                if (becameEnabled)
                {
                    _logger.LogInformation("BlacklistSynchronizer enabled, starting job");
                    await _jobManagementService.StartJob(JobType.BlacklistSynchronizer, null, newConfig.CronExpression);
                    await _jobManagementService.TriggerJobOnce(JobType.BlacklistSynchronizer);
                }
                else
                {
                    _logger.LogInformation("BlacklistSynchronizer disabled, stopping the job");
                    await _jobManagementService.StopJob(JobType.BlacklistSynchronizer);
                }
            }
            else if (pathChanged && oldConfig.Enabled)
            {
                _logger.LogDebug("BlacklistSynchronizer path changed");
                await _jobManagementService.TriggerJobOnce(JobType.BlacklistSynchronizer);
            }

            return Ok(new { Message = "BlacklistSynchronizer configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save BlacklistSync configuration");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("sonarr")]
    public async Task<IActionResult> UpdateSonarrConfig([FromBody] UpdateSonarrConfigDto newConfigDto)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get existing config
            var config = await _dataContext.ArrConfigs
                .FirstAsync(x => x.Type == InstanceType.Sonarr);

            config.FailedImportMaxStrikes = newConfigDto.FailedImportMaxStrikes;

            // Validate the configuration
            config.Validate();

            // Persist the configuration
            await _dataContext.SaveChangesAsync();

            return Ok(new { Message = "Sonarr configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Sonarr configuration");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("radarr")]
    public async Task<IActionResult> UpdateRadarrConfig([FromBody] UpdateRadarrConfigDto newConfigDto)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get existing config
            var config = await _dataContext.ArrConfigs
                .FirstAsync(x => x.Type == InstanceType.Radarr);

            config.FailedImportMaxStrikes = newConfigDto.FailedImportMaxStrikes;

            // Validate the configuration
            config.Validate();

            // Persist the configuration
            await _dataContext.SaveChangesAsync();

            return Ok(new { Message = "Radarr configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Radarr configuration");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("lidarr")]
    public async Task<IActionResult> UpdateLidarrConfig([FromBody] UpdateLidarrConfigDto newConfigDto)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get existing config
            var config = await _dataContext.ArrConfigs
                .FirstAsync(x => x.Type == InstanceType.Lidarr);

            config.FailedImportMaxStrikes = newConfigDto.FailedImportMaxStrikes;

            // Validate the configuration
            config.Validate();

            // Persist the configuration
            await _dataContext.SaveChangesAsync();

            return Ok(new { Message = "Lidarr configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Lidarr configuration");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("readarr")]
    public async Task<IActionResult> UpdateReadarrConfig([FromBody] UpdateReadarrConfigDto newConfigDto)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get existing config
            var config = await _dataContext.ArrConfigs
                .FirstAsync(x => x.Type == InstanceType.Readarr);

            config.FailedImportMaxStrikes = newConfigDto.FailedImportMaxStrikes;

            // Validate the configuration
            config.Validate();

            // Persist the configuration
            await _dataContext.SaveChangesAsync();

            return Ok(new { Message = "Readarr configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Readarr configuration");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("whisparr")]
    public async Task<IActionResult> UpdateWhisparrConfig([FromBody] UpdateWhisparrConfigDto newConfigDto)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get existing config
            var config = await _dataContext.ArrConfigs
                .FirstAsync(x => x.Type == InstanceType.Whisparr);

            config.FailedImportMaxStrikes = newConfigDto.FailedImportMaxStrikes;

            // Validate the configuration
            config.Validate();

            // Persist the configuration
            await _dataContext.SaveChangesAsync();

            return Ok(new { Message = "Whisparr configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Whisparr configuration");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
    
    /// <summary>
    /// Updates a job schedule based on configuration changes
    /// </summary>
    /// <param name="config">The job configuration</param>
    /// <param name="jobType">The type of job to update</param>
    private async Task UpdateJobSchedule(IJobConfig config, JobType jobType)
    {
        if (config.Enabled)
        {
            // Get the cron expression based on the specific config type
            if (!string.IsNullOrEmpty(config.CronExpression))
            {
                // If the job is enabled, update its schedule with the configured cron expression
                _logger.LogInformation("{name} is enabled, updating job schedule with cron expression: {CronExpression}",
                    jobType.ToString(), config.CronExpression);

                // Create a Quartz job schedule with the cron expression
                await _jobManagementService.StartJob(jobType, null, config.CronExpression);
            }
            else
            {
                _logger.LogWarning("{name} is enabled, but no cron expression was found in the configuration", jobType.ToString());
            }

            return;
        }

        // If the job is disabled, stop it
        _logger.LogInformation("{name} is disabled, stopping the job", jobType.ToString());
        await _jobManagementService.StopJob(jobType);
    }

    [HttpPost("sonarr/instances")]
    public async Task<IActionResult> CreateSonarrInstance([FromBody] CreateArrInstanceDto newInstance)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Sonarr config to add the instance to
            var config = await _dataContext.ArrConfigs
                .FirstAsync(x => x.Type == InstanceType.Sonarr);

            // Create the new instance
            var instance = new ArrInstance
            {
                Enabled = newInstance.Enabled,
                Name = newInstance.Name,
                Url = new Uri(newInstance.Url),
                ApiKey = newInstance.ApiKey,
                ArrConfigId = config.Id,
            };
            
            // Add to the config's instances collection
            // config.Instances.Add(instance);
            await _dataContext.ArrInstances.AddAsync(instance);
            // Save changes
            await _dataContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSonarrConfig), new { id = instance.Id }, instance.Adapt<ArrInstanceDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Sonarr instance");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("sonarr/instances/{id}")]
    public async Task<IActionResult> UpdateSonarrInstance(Guid id, [FromBody] CreateArrInstanceDto updatedInstance)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Sonarr config and find the instance
            var config = await _dataContext.ArrConfigs
                .Include(c => c.Instances)
                .FirstAsync(x => x.Type == InstanceType.Sonarr);

            var instance = config.Instances.FirstOrDefault(i => i.Id == id);
            if (instance == null)
            {
                return NotFound($"Sonarr instance with ID {id} not found");
            }

            // Update the instance properties
            instance.Enabled = updatedInstance.Enabled;
            instance.Name = updatedInstance.Name;
            instance.Url = new Uri(updatedInstance.Url);
            instance.ApiKey = updatedInstance.ApiKey;

            await _dataContext.SaveChangesAsync();

            return Ok(instance.Adapt<ArrInstanceDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Sonarr instance with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpDelete("sonarr/instances/{id}")]
    public async Task<IActionResult> DeleteSonarrInstance(Guid id)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Sonarr config and find the instance
            var config = await _dataContext.ArrConfigs
                .Include(c => c.Instances)
                .FirstAsync(x => x.Type == InstanceType.Sonarr);

            var instance = config.Instances.FirstOrDefault(i => i.Id == id);
            if (instance == null)
            {
                return NotFound($"Sonarr instance with ID {id} not found");
            }

            // Remove the instance
            config.Instances.Remove(instance);
            await _dataContext.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Sonarr instance with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPost("radarr/instances")]
    public async Task<IActionResult> CreateRadarrInstance([FromBody] CreateArrInstanceDto newInstance)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Radarr config to add the instance to
            var config = await _dataContext.ArrConfigs
                .FirstAsync(x => x.Type == InstanceType.Radarr);

            // Create the new instance
            var instance = new ArrInstance
            {
                Enabled = newInstance.Enabled,
                Name = newInstance.Name,
                Url = new Uri(newInstance.Url),
                ApiKey = newInstance.ApiKey,
                ArrConfigId = config.Id,
            };
            
            // Add to the config's instances collection
            await _dataContext.ArrInstances.AddAsync(instance);
            // Save changes
            await _dataContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetRadarrConfig), new { id = instance.Id }, instance.Adapt<ArrInstanceDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Radarr instance");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("radarr/instances/{id}")]
    public async Task<IActionResult> UpdateRadarrInstance(Guid id, [FromBody] CreateArrInstanceDto updatedInstance)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Radarr config and find the instance
            var config = await _dataContext.ArrConfigs
                .Include(c => c.Instances)
                .FirstAsync(x => x.Type == InstanceType.Radarr);

            var instance = config.Instances.FirstOrDefault(i => i.Id == id);
            if (instance == null)
            {
                return NotFound($"Radarr instance with ID {id} not found");
            }

            // Update the instance properties
            instance.Enabled = updatedInstance.Enabled;
            instance.Name = updatedInstance.Name;
            instance.Url = new Uri(updatedInstance.Url);
            instance.ApiKey = updatedInstance.ApiKey;

            await _dataContext.SaveChangesAsync();

            return Ok(instance.Adapt<ArrInstanceDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Radarr instance with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpDelete("radarr/instances/{id}")]
    public async Task<IActionResult> DeleteRadarrInstance(Guid id)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Radarr config and find the instance
            var config = await _dataContext.ArrConfigs
                .Include(c => c.Instances)
                .FirstAsync(x => x.Type == InstanceType.Radarr);
            
            var instance = config.Instances.FirstOrDefault(i => i.Id == id);
            if (instance == null)
            {
                return NotFound($"Radarr instance with ID {id} not found");
            }
            
            // Remove the instance
            config.Instances.Remove(instance);
            await _dataContext.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Radarr instance with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPost("lidarr/instances")]
    public async Task<IActionResult> CreateLidarrInstance([FromBody] CreateArrInstanceDto newInstance)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Lidarr config to add the instance to
            var config = await _dataContext.ArrConfigs
                .FirstAsync(x => x.Type == InstanceType.Lidarr);

            // Create the new instance
            var instance = new ArrInstance
            {
                Enabled = newInstance.Enabled,
                Name = newInstance.Name,
                Url = new Uri(newInstance.Url),
                ApiKey = newInstance.ApiKey,
                ArrConfigId = config.Id,
            };
            
            // Add to the config's instances collection
            // config.Instances.Add(instance);
            await _dataContext.ArrInstances.AddAsync(instance);
            // Save changes
            await _dataContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetLidarrConfig), new { id = instance.Id }, instance.Adapt<ArrInstanceDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Lidarr instance");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("lidarr/instances/{id}")]
    public async Task<IActionResult> UpdateLidarrInstance(Guid id, [FromBody] CreateArrInstanceDto updatedInstance)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Lidarr config and find the instance
            var config = await _dataContext.ArrConfigs
                .Include(c => c.Instances)
                .FirstAsync(x => x.Type == InstanceType.Lidarr);

            var instance = config.Instances.FirstOrDefault(i => i.Id == id);
            if (instance == null)
            {
                return NotFound($"Lidarr instance with ID {id} not found");
            }

            // Update the instance properties
            instance.Enabled = updatedInstance.Enabled;
            instance.Name = updatedInstance.Name;
            instance.Url = new Uri(updatedInstance.Url);
            instance.ApiKey = updatedInstance.ApiKey;

            await _dataContext.SaveChangesAsync();

            return Ok(instance.Adapt<ArrInstanceDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Lidarr instance with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpDelete("lidarr/instances/{id}")]
    public async Task<IActionResult> DeleteLidarrInstance(Guid id)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Lidarr config and find the instance
            var config = await _dataContext.ArrConfigs
                .Include(c => c.Instances)
                .FirstAsync(x => x.Type == InstanceType.Lidarr);

            var instance = config.Instances.FirstOrDefault(i => i.Id == id);
            if (instance == null)
            {
                return NotFound($"Lidarr instance with ID {id} not found");
            }

            // Remove the instance
            config.Instances.Remove(instance);
            await _dataContext.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Lidarr instance with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPost("readarr/instances")]
    public async Task<IActionResult> CreateReadarrInstance([FromBody] CreateArrInstanceDto newInstance)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Readarr config to add the instance to
            var config = await _dataContext.ArrConfigs
                .FirstAsync(x => x.Type == InstanceType.Readarr);

            // Create the new instance
            var instance = new ArrInstance
            {
                Enabled = newInstance.Enabled,
                Name = newInstance.Name,
                Url = new Uri(newInstance.Url),
                ApiKey = newInstance.ApiKey,
                ArrConfigId = config.Id,
            };
            
            // Add to the config's instances collection
            await _dataContext.ArrInstances.AddAsync(instance);
            // Save changes
            await _dataContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetReadarrConfig), new { id = instance.Id }, instance.Adapt<ArrInstanceDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Readarr instance");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("readarr/instances/{id}")]
    public async Task<IActionResult> UpdateReadarrInstance(Guid id, [FromBody] CreateArrInstanceDto updatedInstance)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Readarr config and find the instance
            var config = await _dataContext.ArrConfigs
                .Include(c => c.Instances)
                .FirstAsync(x => x.Type == InstanceType.Readarr);

            var instance = config.Instances.FirstOrDefault(i => i.Id == id);
            if (instance == null)
            {
                return NotFound($"Readarr instance with ID {id} not found");
            }

            // Update the instance properties
            instance.Enabled = updatedInstance.Enabled;
            instance.Name = updatedInstance.Name;
            instance.Url = new Uri(updatedInstance.Url);
            instance.ApiKey = updatedInstance.ApiKey;

            await _dataContext.SaveChangesAsync();

            return Ok(instance.Adapt<ArrInstanceDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Readarr instance with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpDelete("readarr/instances/{id}")]
    public async Task<IActionResult> DeleteReadarrInstance(Guid id)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Readarr config and find the instance
            var config = await _dataContext.ArrConfigs
                .Include(c => c.Instances)
                .FirstAsync(x => x.Type == InstanceType.Readarr);

            var instance = config.Instances.FirstOrDefault(i => i.Id == id);
            if (instance == null)
            {
                return NotFound($"Readarr instance with ID {id} not found");
            }

            // Remove the instance
            config.Instances.Remove(instance);
            await _dataContext.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Readarr instance with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPost("whisparr/instances")]
    public async Task<IActionResult> CreateWhisparrInstance([FromBody] CreateArrInstanceDto newInstance)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Whisparr config to add the instance to
            var config = await _dataContext.ArrConfigs
                .FirstAsync(x => x.Type == InstanceType.Whisparr);

            // Create the new instance
            var instance = new ArrInstance
            {
                Enabled = newInstance.Enabled,
                Name = newInstance.Name,
                Url = new Uri(newInstance.Url),
                ApiKey = newInstance.ApiKey,
                ArrConfigId = config.Id,
            };
            
            // Add to the config's instances collection
            await _dataContext.ArrInstances.AddAsync(instance);
            // Save changes
            await _dataContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetWhisparrConfig), new { id = instance.Id }, instance.Adapt<ArrInstanceDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Whisparr instance");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("whisparr/instances/{id}")]
    public async Task<IActionResult> UpdateWhisparrInstance(Guid id, [FromBody] CreateArrInstanceDto updatedInstance)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Whisparr config and find the instance
            var config = await _dataContext.ArrConfigs
                .Include(c => c.Instances)
                .FirstAsync(x => x.Type == InstanceType.Whisparr);

            var instance = config.Instances.FirstOrDefault(i => i.Id == id);
            if (instance == null)
            {
                return NotFound($"Whisparr instance with ID {id} not found");
            }

            // Update the instance properties
            instance.Enabled = updatedInstance.Enabled;
            instance.Name = updatedInstance.Name;
            instance.Url = new Uri(updatedInstance.Url);
            instance.ApiKey = updatedInstance.ApiKey;

            await _dataContext.SaveChangesAsync();

            return Ok(instance.Adapt<ArrInstanceDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Whisparr instance with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpDelete("whisparr/instances/{id}")]
    public async Task<IActionResult> DeleteWhisparrInstance(Guid id)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Whisparr config and find the instance
            var config = await _dataContext.ArrConfigs
                .Include(c => c.Instances)
                .FirstAsync(x => x.Type == InstanceType.Whisparr);

            var instance = config.Instances.FirstOrDefault(i => i.Id == id);
            if (instance == null)
            {
                return NotFound($"Whisparr instance with ID {id} not found");
            }

            // Remove the instance
            config.Instances.Remove(instance);
            await _dataContext.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Whisparr instance with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    /// <summary>
    /// Determines what type of logging reconfiguration is needed based on configuration changes
    /// </summary>
    /// <param name="oldConfig">The previous logging configuration</param>
    /// <param name="newConfig">The new logging configuration</param>
    /// <returns>A tuple indicating the type of reconfiguration needed</returns>
    private static (bool LevelOnly, bool FullReconfiguration) HasLoggingConfigurationChanged(LoggingConfig oldConfig, LoggingConfig newConfig)
    {
        // Check if only the log level changed
        bool levelChanged = oldConfig.Level != newConfig.Level;
        
        // Check if other logging properties changed that require full reconfiguration
        bool otherPropertiesChanged = 
            oldConfig.RollingSizeMB != newConfig.RollingSizeMB ||
            oldConfig.RetainedFileCount != newConfig.RetainedFileCount ||
            oldConfig.TimeLimitHours != newConfig.TimeLimitHours ||
            oldConfig.ArchiveEnabled != newConfig.ArchiveEnabled ||
            oldConfig.ArchiveRetainedCount != newConfig.ArchiveRetainedCount ||
            oldConfig.ArchiveTimeLimitHours != newConfig.ArchiveTimeLimitHours;

        if (otherPropertiesChanged)
        {
            // Full reconfiguration needed (includes level change if any)
            return (false, true);
        }
        
        if (levelChanged)
        {
            // Only level changed, simple level update is sufficient
            return (true, false);
        }
        
        // No logging configuration changes
        return (false, false);
    }
}
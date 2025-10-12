using Cleanuparr.Api.Features.Notifications.Contracts.Requests;
using Cleanuparr.Api.Features.Notifications.Contracts.Responses;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Api.Features.Notifications.Controllers;

[ApiController]
[Route("api/configuration/notification_providers")]
public sealed class NotificationProvidersController : ControllerBase
{
    private readonly ILogger<NotificationProvidersController> _logger;
    private readonly DataContext _dataContext;
    private readonly INotificationConfigurationService _notificationConfigurationService;
    private readonly NotificationService _notificationService;

    public NotificationProvidersController(
        ILogger<NotificationProvidersController> logger,
        DataContext dataContext,
        INotificationConfigurationService notificationConfigurationService,
        NotificationService notificationService)
    {
        _logger = logger;
        _dataContext = dataContext;
        _notificationConfigurationService = notificationConfigurationService;
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotificationProviders()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var providers = await _dataContext.NotificationConfigs
                .Include(p => p.NotifiarrConfiguration)
                .Include(p => p.AppriseConfiguration)
                .Include(p => p.NtfyConfiguration)
                .AsNoTracking()
                .ToListAsync();

            var providerDtos = providers
                .Select(p => new NotificationProviderResponse
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
                        NotificationProviderType.Ntfy => p.NtfyConfiguration ?? new object(),
                        _ => new object()
                    }
                })
                .OrderBy(x => x.Type.ToString())
                .ThenBy(x => x.Name)
                .ToList();

            var response = new NotificationProvidersResponse { Providers = providerDtos };
            return Ok(response);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPost("notifiarr")]
    public async Task<IActionResult> CreateNotifiarrProvider([FromBody] CreateNotifiarrProviderRequest newProvider)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(newProvider.Name))
            {
                return BadRequest("Provider name is required");
            }

            var duplicateConfig = await _dataContext.NotificationConfigs.CountAsync(x => x.Name == newProvider.Name);
            if (duplicateConfig > 0)
            {
                return BadRequest("A provider with this name already exists");
            }

            var notifiarrConfig = new NotifiarrConfig
            {
                ApiKey = newProvider.ApiKey,
                ChannelId = newProvider.ChannelId
            };
            notifiarrConfig.Validate();

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

            _dataContext.NotificationConfigs.Add(provider);
            await _dataContext.SaveChangesAsync();

            await _notificationConfigurationService.InvalidateCacheAsync();

            var providerDto = MapProvider(provider);
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

    [HttpPost("apprise")]
    public async Task<IActionResult> CreateAppriseProvider([FromBody] CreateAppriseProviderRequest newProvider)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(newProvider.Name))
            {
                return BadRequest("Provider name is required");
            }

            var duplicateConfig = await _dataContext.NotificationConfigs.CountAsync(x => x.Name == newProvider.Name);
            if (duplicateConfig > 0)
            {
                return BadRequest("A provider with this name already exists");
            }

            var appriseConfig = new AppriseConfig
            {
                Url = newProvider.Url,
                Key = newProvider.Key,
                Tags = newProvider.Tags
            };
            appriseConfig.Validate();

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

            _dataContext.NotificationConfigs.Add(provider);
            await _dataContext.SaveChangesAsync();

            await _notificationConfigurationService.InvalidateCacheAsync();

            var providerDto = MapProvider(provider);
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

    [HttpPost("ntfy")]
    public async Task<IActionResult> CreateNtfyProvider([FromBody] CreateNtfyProviderRequest newProvider)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(newProvider.Name))
            {
                return BadRequest("Provider name is required");
            }

            var duplicateConfig = await _dataContext.NotificationConfigs.CountAsync(x => x.Name == newProvider.Name);
            if (duplicateConfig > 0)
            {
                return BadRequest("A provider with this name already exists");
            }

            var ntfyConfig = new NtfyConfig
            {
                ServerUrl = newProvider.ServerUrl,
                Topics = newProvider.Topics,
                AuthenticationType = newProvider.AuthenticationType,
                Username = newProvider.Username,
                Password = newProvider.Password,
                AccessToken = newProvider.AccessToken,
                Priority = newProvider.Priority,
                Tags = newProvider.Tags
            };
            ntfyConfig.Validate();

            var provider = new NotificationConfig
            {
                Name = newProvider.Name,
                Type = NotificationProviderType.Ntfy,
                IsEnabled = newProvider.IsEnabled,
                OnFailedImportStrike = newProvider.OnFailedImportStrike,
                OnStalledStrike = newProvider.OnStalledStrike,
                OnSlowStrike = newProvider.OnSlowStrike,
                OnQueueItemDeleted = newProvider.OnQueueItemDeleted,
                OnDownloadCleaned = newProvider.OnDownloadCleaned,
                OnCategoryChanged = newProvider.OnCategoryChanged,
                NtfyConfiguration = ntfyConfig
            };

            _dataContext.NotificationConfigs.Add(provider);
            await _dataContext.SaveChangesAsync();

            await _notificationConfigurationService.InvalidateCacheAsync();

            var providerDto = MapProvider(provider);
            return CreatedAtAction(nameof(GetNotificationProviders), new { id = provider.Id }, providerDto);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Ntfy provider");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("notifiarr/{id:guid}")]
    public async Task<IActionResult> UpdateNotifiarrProvider(Guid id, [FromBody] UpdateNotifiarrProviderRequest updatedProvider)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var existingProvider = await _dataContext.NotificationConfigs
                .Include(p => p.NotifiarrConfiguration)
                .FirstOrDefaultAsync(p => p.Id == id && p.Type == NotificationProviderType.Notifiarr);

            if (existingProvider == null)
            {
                return NotFound($"Notifiarr provider with ID {id} not found");
            }

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

            var notifiarrConfig = new NotifiarrConfig
            {
                ApiKey = updatedProvider.ApiKey,
                ChannelId = updatedProvider.ChannelId
            };

            if (existingProvider.NotifiarrConfiguration != null)
            {
                notifiarrConfig = notifiarrConfig with { Id = existingProvider.NotifiarrConfiguration.Id };
            }
            notifiarrConfig.Validate();

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

            _dataContext.NotificationConfigs.Remove(existingProvider);
            _dataContext.NotificationConfigs.Add(newProvider);

            await _dataContext.SaveChangesAsync();
            await _notificationConfigurationService.InvalidateCacheAsync();

            var providerDto = MapProvider(newProvider);
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

    [HttpPut("apprise/{id:guid}")]
    public async Task<IActionResult> UpdateAppriseProvider(Guid id, [FromBody] UpdateAppriseProviderRequest updatedProvider)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var existingProvider = await _dataContext.NotificationConfigs
                .Include(p => p.AppriseConfiguration)
                .FirstOrDefaultAsync(p => p.Id == id && p.Type == NotificationProviderType.Apprise);

            if (existingProvider == null)
            {
                return NotFound($"Apprise provider with ID {id} not found");
            }

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

            var appriseConfig = new AppriseConfig
            {
                Url = updatedProvider.Url,
                Key = updatedProvider.Key,
                Tags = updatedProvider.Tags
            };

            if (existingProvider.AppriseConfiguration != null)
            {
                appriseConfig = appriseConfig with { Id = existingProvider.AppriseConfiguration.Id };
            }
            appriseConfig.Validate();

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

            _dataContext.NotificationConfigs.Remove(existingProvider);
            _dataContext.NotificationConfigs.Add(newProvider);

            await _dataContext.SaveChangesAsync();
            await _notificationConfigurationService.InvalidateCacheAsync();

            var providerDto = MapProvider(newProvider);
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

    [HttpPut("ntfy/{id:guid}")]
    public async Task<IActionResult> UpdateNtfyProvider(Guid id, [FromBody] UpdateNtfyProviderRequest updatedProvider)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var existingProvider = await _dataContext.NotificationConfigs
                .Include(p => p.NtfyConfiguration)
                .FirstOrDefaultAsync(p => p.Id == id && p.Type == NotificationProviderType.Ntfy);

            if (existingProvider == null)
            {
                return NotFound($"Ntfy provider with ID {id} not found");
            }

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

            var ntfyConfig = new NtfyConfig
            {
                ServerUrl = updatedProvider.ServerUrl,
                Topics = updatedProvider.Topics,
                AuthenticationType = updatedProvider.AuthenticationType,
                Username = updatedProvider.Username,
                Password = updatedProvider.Password,
                AccessToken = updatedProvider.AccessToken,
                Priority = updatedProvider.Priority,
                Tags = updatedProvider.Tags
            };

            if (existingProvider.NtfyConfiguration != null)
            {
                ntfyConfig = ntfyConfig with { Id = existingProvider.NtfyConfiguration.Id };
            }
            ntfyConfig.Validate();

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
                NtfyConfiguration = ntfyConfig,
                UpdatedAt = DateTime.UtcNow
            };

            _dataContext.NotificationConfigs.Remove(existingProvider);
            _dataContext.NotificationConfigs.Add(newProvider);

            await _dataContext.SaveChangesAsync();
            await _notificationConfigurationService.InvalidateCacheAsync();

            var providerDto = MapProvider(newProvider);
            return Ok(providerDto);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Ntfy provider with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNotificationProvider(Guid id)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var existingProvider = await _dataContext.NotificationConfigs
                .Include(p => p.NotifiarrConfiguration)
                .Include(p => p.AppriseConfiguration)
                .Include(p => p.NtfyConfiguration)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (existingProvider == null)
            {
                return NotFound($"Notification provider with ID {id} not found");
            }

            _dataContext.NotificationConfigs.Remove(existingProvider);
            await _dataContext.SaveChangesAsync();

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

    [HttpPost("notifiarr/test")]
    public async Task<IActionResult> TestNotifiarrProvider([FromBody] TestNotifiarrProviderRequest testRequest)
    {
        try
        {
            var notifiarrConfig = new NotifiarrConfig
            {
                ApiKey = testRequest.ApiKey,
                ChannelId = testRequest.ChannelId
            };
            notifiarrConfig.Validate();

            var providerDto = new NotificationProviderDto
            {
                Id = Guid.NewGuid(),
                Name = "Test Provider",
                Type = NotificationProviderType.Notifiarr,
                IsEnabled = true,
                Events = new NotificationEventFlags
                {
                    OnFailedImportStrike = true,
                    OnStalledStrike = false,
                    OnSlowStrike = false,
                    OnQueueItemDeleted = false,
                    OnDownloadCleaned = false,
                    OnCategoryChanged = false
                },
                Configuration = notifiarrConfig
            };

            await _notificationService.SendTestNotificationAsync(providerDto);
            return Ok(new { Message = "Test notification sent successfully", Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test Notifiarr provider");
            throw;
        }
    }

    [HttpPost("apprise/test")]
    public async Task<IActionResult> TestAppriseProvider([FromBody] TestAppriseProviderRequest testRequest)
    {
        try
        {
            var appriseConfig = new AppriseConfig
            {
                Url = testRequest.Url,
                Key = testRequest.Key,
                Tags = testRequest.Tags
            };
            appriseConfig.Validate();

            var providerDto = new NotificationProviderDto
            {
                Id = Guid.NewGuid(),
                Name = "Test Provider",
                Type = NotificationProviderType.Apprise,
                IsEnabled = true,
                Events = new NotificationEventFlags
                {
                    OnFailedImportStrike = true,
                    OnStalledStrike = false,
                    OnSlowStrike = false,
                    OnQueueItemDeleted = false,
                    OnDownloadCleaned = false,
                    OnCategoryChanged = false
                },
                Configuration = appriseConfig
            };

            await _notificationService.SendTestNotificationAsync(providerDto);
            return Ok(new { Message = "Test notification sent successfully", Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test Apprise provider");
            throw;
        }
    }

    [HttpPost("ntfy/test")]
    public async Task<IActionResult> TestNtfyProvider([FromBody] TestNtfyProviderRequest testRequest)
    {
        try
        {
            var ntfyConfig = new NtfyConfig
            {
                ServerUrl = testRequest.ServerUrl,
                Topics = testRequest.Topics,
                AuthenticationType = testRequest.AuthenticationType,
                Username = testRequest.Username,
                Password = testRequest.Password,
                AccessToken = testRequest.AccessToken,
                Priority = testRequest.Priority,
                Tags = testRequest.Tags
            };
            ntfyConfig.Validate();

            var providerDto = new NotificationProviderDto
            {
                Id = Guid.NewGuid(),
                Name = "Test Provider",
                Type = NotificationProviderType.Ntfy,
                IsEnabled = true,
                Events = new NotificationEventFlags
                {
                    OnFailedImportStrike = true,
                    OnStalledStrike = false,
                    OnSlowStrike = false,
                    OnQueueItemDeleted = false,
                    OnDownloadCleaned = false,
                    OnCategoryChanged = false
                },
                Configuration = ntfyConfig
            };

            await _notificationService.SendTestNotificationAsync(providerDto);
            return Ok(new { Message = "Test notification sent successfully", Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test Ntfy provider");
            throw;
        }
    }

    private static NotificationProviderResponse MapProvider(NotificationConfig provider)
    {
        return new NotificationProviderResponse
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
            Configuration = provider.Type switch
            {
                NotificationProviderType.Notifiarr => provider.NotifiarrConfiguration ?? new object(),
                NotificationProviderType.Apprise => provider.AppriseConfiguration ?? new object(),
                NotificationProviderType.Ntfy => provider.NtfyConfiguration ?? new object(),
                _ => new object()
            }
        };
    }
}

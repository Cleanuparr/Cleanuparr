using System.Net;
using Cleanuparr.Api.Features.Notifications.Contracts.Requests;
using Cleanuparr.Api.Features.Notifications.Contracts.Responses;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Infrastructure.Features.Notifications.Discord;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Features.Notifications.Telegram;
using Cleanuparr.Infrastructure.Features.Notifications.Gotify;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Features.Notifications.Controllers;

[ApiController]
[Route("api/configuration/notification_providers")]
public sealed class NotificationProvidersController : ControllerBase
{
    private readonly ILogger<NotificationProvidersController> _logger;
    private readonly DataContext _dataContext;
    private readonly INotificationConfigurationService _notificationConfigurationService;
    private readonly NotificationService _notificationService;
    private readonly IAppriseCliDetector _appriseCliDetector;

    public NotificationProvidersController(
        ILogger<NotificationProvidersController> logger,
        DataContext dataContext,
        INotificationConfigurationService notificationConfigurationService,
        NotificationService notificationService,
        IAppriseCliDetector appriseCliDetector)
    {
        _logger = logger;
        _dataContext = dataContext;
        _notificationConfigurationService = notificationConfigurationService;
        _notificationService = notificationService;
        _appriseCliDetector = appriseCliDetector;
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
                .Include(p => p.PushoverConfiguration)
                .Include(p => p.TelegramConfiguration)
                .Include(p => p.DiscordConfiguration)
                .Include(p => p.GotifyConfiguration)
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
                        NotificationProviderType.Pushover => p.PushoverConfiguration ?? new object(),
                        NotificationProviderType.Telegram => p.TelegramConfiguration ?? new object(),
                        NotificationProviderType.Discord => p.DiscordConfiguration ?? new object(),
                        NotificationProviderType.Gotify => p.GotifyConfiguration ?? new object(),
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

    [HttpGet("apprise/cli-status")]
    public async Task<IActionResult> GetAppriseCliStatus()
    {
        string? version = await _appriseCliDetector.GetAppriseVersionAsync();

        return Ok(new
        {
            Available = version is not null,
            Version = version
        });
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

            if (newProvider.ApiKey.IsPlaceholder())
            {
                return BadRequest("API key cannot be a placeholder value");
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

            if (newProvider.Key.IsPlaceholder())
            {
                return BadRequest("Key cannot be a placeholder value");
            }

            if (newProvider.ServiceUrls.IsPlaceholder())
            {
                return BadRequest("Service URLs cannot be a placeholder value");
            }

            var appriseConfig = new AppriseConfig
            {
                Mode = newProvider.Mode,
                Url = newProvider.Url,
                Key = newProvider.Key,
                Tags = newProvider.Tags,
                ServiceUrls = newProvider.ServiceUrls
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

            if (newProvider.Password.IsPlaceholder())
            {
                return BadRequest("Password cannot be a placeholder value");
            }

            if (newProvider.AccessToken.IsPlaceholder())
            {
                return BadRequest("Access token cannot be a placeholder value");
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

    [HttpPost("telegram")]
    public async Task<IActionResult> CreateTelegramProvider([FromBody] CreateTelegramProviderRequest newProvider)
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

            if (newProvider.BotToken.IsPlaceholder())
            {
                return BadRequest("Bot token cannot be a placeholder value");
            }

            var telegramConfig = new TelegramConfig
            {
                BotToken = newProvider.BotToken,
                ChatId = newProvider.ChatId,
                TopicId = newProvider.TopicId,
                SendSilently = newProvider.SendSilently
            };
            telegramConfig.Validate();

            var provider = new NotificationConfig
            {
                Name = newProvider.Name,
                Type = NotificationProviderType.Telegram,
                IsEnabled = newProvider.IsEnabled,
                OnFailedImportStrike = newProvider.OnFailedImportStrike,
                OnStalledStrike = newProvider.OnStalledStrike,
                OnSlowStrike = newProvider.OnSlowStrike,
                OnQueueItemDeleted = newProvider.OnQueueItemDeleted,
                OnDownloadCleaned = newProvider.OnDownloadCleaned,
                OnCategoryChanged = newProvider.OnCategoryChanged,
                TelegramConfiguration = telegramConfig
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
            _logger.LogError(ex, "Failed to create Telegram provider");
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
                ApiKey = updatedProvider.ApiKey.IsPlaceholder()
                    ? existingProvider.NotifiarrConfiguration!.ApiKey
                    : updatedProvider.ApiKey,
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
                Mode = updatedProvider.Mode,
                Url = updatedProvider.Url,
                Key = updatedProvider.Key.IsPlaceholder()
                    ? existingProvider.AppriseConfiguration!.Key
                    : updatedProvider.Key,
                Tags = updatedProvider.Tags,
                ServiceUrls = updatedProvider.ServiceUrls.IsPlaceholder()
                    ? existingProvider.AppriseConfiguration!.ServiceUrls
                    : updatedProvider.ServiceUrls
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
                Password = updatedProvider.Password.IsPlaceholder()
                    ? existingProvider.NtfyConfiguration!.Password
                    : updatedProvider.Password,
                AccessToken = updatedProvider.AccessToken.IsPlaceholder()
                    ? existingProvider.NtfyConfiguration!.AccessToken
                    : updatedProvider.AccessToken,
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

    [HttpPut("telegram/{id:guid}")]
    public async Task<IActionResult> UpdateTelegramProvider(Guid id, [FromBody] UpdateTelegramProviderRequest updatedProvider)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var existingProvider = await _dataContext.NotificationConfigs
                .Include(p => p.TelegramConfiguration)
                .FirstOrDefaultAsync(p => p.Id == id && p.Type == NotificationProviderType.Telegram);

            if (existingProvider == null)
            {
                return NotFound($"Telegram provider with ID {id} not found");
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

            var telegramConfig = new TelegramConfig
            {
                BotToken = updatedProvider.BotToken.IsPlaceholder()
                    ? existingProvider.TelegramConfiguration!.BotToken
                    : updatedProvider.BotToken,
                ChatId = updatedProvider.ChatId,
                TopicId = updatedProvider.TopicId,
                SendSilently = updatedProvider.SendSilently
            };

            if (existingProvider.TelegramConfiguration != null)
            {
                telegramConfig = telegramConfig with { Id = existingProvider.TelegramConfiguration.Id };
            }
            telegramConfig.Validate();

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
                TelegramConfiguration = telegramConfig,
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
            _logger.LogError(ex, "Failed to update Telegram provider with ID {Id}", id);
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
                .Include(p => p.PushoverConfiguration)
                .Include(p => p.TelegramConfiguration)
                .Include(p => p.DiscordConfiguration)
                .Include(p => p.GotifyConfiguration)
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
            var apiKey = testRequest.ApiKey;

            if (apiKey.IsPlaceholder())
            {
                var existing = await GetExistingProviderConfig<NotifiarrConfig>(
                    testRequest.ProviderId, NotificationProviderType.Notifiarr, p => p.NotifiarrConfiguration);

                if (existing is null)
                {
                    return BadRequest(new { Message = "API key cannot be a placeholder value" });
                }

                apiKey = existing.ApiKey;
            }

            var notifiarrConfig = new NotifiarrConfig
            {
                ApiKey = apiKey,
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
            return Ok(new { Message = "Test notification sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test Notifiarr provider");
            return BadRequest(new { Message = $"Test failed: {ex.Message}" });
        }
    }

    [HttpPost("apprise/test")]
    public async Task<IActionResult> TestAppriseProvider([FromBody] TestAppriseProviderRequest testRequest)
    {
        try
        {
            var key = testRequest.Key;
            var serviceUrls = testRequest.ServiceUrls;

            if (key.IsPlaceholder() || serviceUrls.IsPlaceholder())
            {
                var existing = await GetExistingProviderConfig<AppriseConfig>(
                    testRequest.ProviderId, NotificationProviderType.Apprise, p => p.AppriseConfiguration);

                if (existing is null)
                {
                    return BadRequest(new { Message = "Sensitive fields cannot be placeholder values" });
                }

                if (key.IsPlaceholder())
                {
                    key = existing.Key;
                }

                if (serviceUrls.IsPlaceholder())
                {
                    serviceUrls = existing.ServiceUrls;
                }
            }

            var appriseConfig = new AppriseConfig
            {
                Mode = testRequest.Mode,
                Url = testRequest.Url,
                Key = key,
                Tags = testRequest.Tags,
                ServiceUrls = serviceUrls
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
            return Ok(new { Message = "Test notification sent successfully" });
        }
        catch (AppriseException exception)
        {
            return StatusCode((int)HttpStatusCode.InternalServerError, exception.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test Apprise provider");
            return BadRequest(new { Message = $"Test failed: {ex.Message}" });
        }
    }

    [HttpPost("ntfy/test")]
    public async Task<IActionResult> TestNtfyProvider([FromBody] TestNtfyProviderRequest testRequest)
    {
        try
        {
            var password = testRequest.Password;
            var accessToken = testRequest.AccessToken;

            if (password.IsPlaceholder() || accessToken.IsPlaceholder())
            {
                var existing = await GetExistingProviderConfig<NtfyConfig>(
                    testRequest.ProviderId, NotificationProviderType.Ntfy, p => p.NtfyConfiguration);

                if (existing is null)
                {
                    return BadRequest(new { Message = "Sensitive fields cannot be placeholder values" });
                }

                if (password.IsPlaceholder())
                {
                    password = existing.Password;
                }

                if (accessToken.IsPlaceholder())
                {
                    accessToken = existing.AccessToken;
                }
            }

            var ntfyConfig = new NtfyConfig
            {
                ServerUrl = testRequest.ServerUrl,
                Topics = testRequest.Topics,
                AuthenticationType = testRequest.AuthenticationType,
                Username = testRequest.Username,
                Password = password,
                AccessToken = accessToken,
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
            return Ok(new { Message = "Test notification sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test Ntfy provider");
            return BadRequest(new { Message = $"Test failed: {ex.Message}" });
        }
    }

    [HttpPost("telegram/test")]
    public async Task<IActionResult> TestTelegramProvider([FromBody] TestTelegramProviderRequest testRequest)
    {
        try
        {
            var botToken = testRequest.BotToken;

            if (botToken.IsPlaceholder())
            {
                var existing = await GetExistingProviderConfig<TelegramConfig>(
                    testRequest.ProviderId, NotificationProviderType.Telegram, p => p.TelegramConfiguration);

                if (existing is null)
                {
                    return BadRequest(new { Message = "Bot token cannot be a placeholder value" });
                }

                botToken = existing.BotToken;
            }

            var telegramConfig = new TelegramConfig
            {
                BotToken = botToken,
                ChatId = testRequest.ChatId,
                TopicId = testRequest.TopicId,
                SendSilently = testRequest.SendSilently
            };
            telegramConfig.Validate();

            var providerDto = new NotificationProviderDto
            {
                Id = Guid.NewGuid(),
                Name = "Test Provider",
                Type = NotificationProviderType.Telegram,
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
                Configuration = telegramConfig
            };

            await _notificationService.SendTestNotificationAsync(providerDto);
            return Ok(new { Message = "Test notification sent successfully" });
        }
        catch (TelegramException ex)
        {
            _logger.LogWarning(ex, "Failed to test Telegram provider");
            return BadRequest(new { Message = $"Test failed: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test Telegram provider");
            return BadRequest(new { Message = $"Test failed: {ex.Message}" });
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
                NotificationProviderType.Pushover => provider.PushoverConfiguration ?? new object(),
                NotificationProviderType.Telegram => provider.TelegramConfiguration ?? new object(),
                NotificationProviderType.Discord => provider.DiscordConfiguration ?? new object(),
                NotificationProviderType.Gotify => provider.GotifyConfiguration ?? new object(),
                _ => new object()
            }
        };
    }

    [HttpPost("discord")]
    public async Task<IActionResult> CreateDiscordProvider([FromBody] CreateDiscordProviderRequest newProvider)
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

            if (newProvider.WebhookUrl.IsPlaceholder())
            {
                return BadRequest("Webhook URL cannot be a placeholder value");
            }

            var discordConfig = new DiscordConfig
            {
                WebhookUrl = newProvider.WebhookUrl,
                Username = newProvider.Username,
                AvatarUrl = newProvider.AvatarUrl
            };
            discordConfig.Validate();

            var provider = new NotificationConfig
            {
                Name = newProvider.Name,
                Type = NotificationProviderType.Discord,
                IsEnabled = newProvider.IsEnabled,
                OnFailedImportStrike = newProvider.OnFailedImportStrike,
                OnStalledStrike = newProvider.OnStalledStrike,
                OnSlowStrike = newProvider.OnSlowStrike,
                OnQueueItemDeleted = newProvider.OnQueueItemDeleted,
                OnDownloadCleaned = newProvider.OnDownloadCleaned,
                OnCategoryChanged = newProvider.OnCategoryChanged,
                DiscordConfiguration = discordConfig
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
            _logger.LogError(ex, "Failed to create Discord provider");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("discord/{id:guid}")]
    public async Task<IActionResult> UpdateDiscordProvider(Guid id, [FromBody] UpdateDiscordProviderRequest updatedProvider)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var existingProvider = await _dataContext.NotificationConfigs
                .Include(p => p.DiscordConfiguration)
                .FirstOrDefaultAsync(p => p.Id == id && p.Type == NotificationProviderType.Discord);

            if (existingProvider == null)
            {
                return NotFound($"Discord provider with ID {id} not found");
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

            var discordConfig = new DiscordConfig
            {
                WebhookUrl = updatedProvider.WebhookUrl.IsPlaceholder()
                    ? existingProvider.DiscordConfiguration!.WebhookUrl
                    : updatedProvider.WebhookUrl,
                Username = updatedProvider.Username,
                AvatarUrl = updatedProvider.AvatarUrl
            };

            if (existingProvider.DiscordConfiguration != null)
            {
                discordConfig = discordConfig with { Id = existingProvider.DiscordConfiguration.Id };
            }
            discordConfig.Validate();

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
                DiscordConfiguration = discordConfig,
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
            _logger.LogError(ex, "Failed to update Discord provider with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPost("discord/test")]
    public async Task<IActionResult> TestDiscordProvider([FromBody] TestDiscordProviderRequest testRequest)
    {
        try
        {
            var webhookUrl = testRequest.WebhookUrl;

            if (webhookUrl.IsPlaceholder())
            {
                var existing = await GetExistingProviderConfig<DiscordConfig>(
                    testRequest.ProviderId, NotificationProviderType.Discord, p => p.DiscordConfiguration);

                if (existing is null)
                {
                    return BadRequest(new { Message = "Webhook URL cannot be a placeholder value" });
                }

                webhookUrl = existing.WebhookUrl;
            }

            var discordConfig = new DiscordConfig
            {
                WebhookUrl = webhookUrl,
                Username = testRequest.Username,
                AvatarUrl = testRequest.AvatarUrl
            };
            discordConfig.Validate();

            var providerDto = new NotificationProviderDto
            {
                Id = Guid.NewGuid(),
                Name = "Test Provider",
                Type = NotificationProviderType.Discord,
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
                Configuration = discordConfig
            };

            await _notificationService.SendTestNotificationAsync(providerDto);
            return Ok(new { Message = "Test notification sent successfully" });
        }
        catch (DiscordException ex)
        {
            _logger.LogWarning(ex, "Failed to test Discord provider");
            return BadRequest(new { Message = $"Test failed: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test Discord provider");
            return BadRequest(new { Message = $"Test failed: {ex.Message}" });
        }
    }

    [HttpPost("pushover")]
    public async Task<IActionResult> CreatePushoverProvider([FromBody] CreatePushoverProviderRequest newProvider)
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

            if (newProvider.ApiToken.IsPlaceholder())
            {
                return BadRequest("API token cannot be a placeholder value");
            }

            if (newProvider.UserKey.IsPlaceholder())
            {
                return BadRequest("User key cannot be a placeholder value");
            }

            var pushoverConfig = new PushoverConfig
            {
                ApiToken = newProvider.ApiToken,
                UserKey = newProvider.UserKey,
                Devices = newProvider.Devices,
                Priority = newProvider.Priority,
                Sound = newProvider.Sound,
                Retry = newProvider.Retry,
                Expire = newProvider.Expire,
                Tags = newProvider.Tags
            };
            pushoverConfig.Validate();

            var provider = new NotificationConfig
            {
                Name = newProvider.Name,
                Type = NotificationProviderType.Pushover,
                IsEnabled = newProvider.IsEnabled,
                OnFailedImportStrike = newProvider.OnFailedImportStrike,
                OnStalledStrike = newProvider.OnStalledStrike,
                OnSlowStrike = newProvider.OnSlowStrike,
                OnQueueItemDeleted = newProvider.OnQueueItemDeleted,
                OnDownloadCleaned = newProvider.OnDownloadCleaned,
                OnCategoryChanged = newProvider.OnCategoryChanged,
                PushoverConfiguration = pushoverConfig
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
            _logger.LogError(ex, "Failed to create Pushover provider");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("pushover/{id:guid}")]
    public async Task<IActionResult> UpdatePushoverProvider(Guid id, [FromBody] UpdatePushoverProviderRequest updatedProvider)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var existingProvider = await _dataContext.NotificationConfigs
                .Include(p => p.PushoverConfiguration)
                .FirstOrDefaultAsync(p => p.Id == id && p.Type == NotificationProviderType.Pushover);

            if (existingProvider == null)
            {
                return NotFound($"Pushover provider with ID {id} not found");
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

            var pushoverConfig = new PushoverConfig
            {
                ApiToken = updatedProvider.ApiToken.IsPlaceholder()
                    ? existingProvider.PushoverConfiguration!.ApiToken
                    : updatedProvider.ApiToken,
                UserKey = updatedProvider.UserKey.IsPlaceholder()
                    ? existingProvider.PushoverConfiguration!.UserKey
                    : updatedProvider.UserKey,
                Devices = updatedProvider.Devices,
                Priority = updatedProvider.Priority,
                Sound = updatedProvider.Sound,
                Retry = updatedProvider.Retry,
                Expire = updatedProvider.Expire,
                Tags = updatedProvider.Tags
            };

            if (existingProvider.PushoverConfiguration != null)
            {
                pushoverConfig = pushoverConfig with { Id = existingProvider.PushoverConfiguration.Id };
            }
            pushoverConfig.Validate();

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
                PushoverConfiguration = pushoverConfig,
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
            _logger.LogError(ex, "Failed to update Pushover provider with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPost("pushover/test")]
    public async Task<IActionResult> TestPushoverProvider([FromBody] TestPushoverProviderRequest testRequest)
    {
        try
        {
            var apiToken = testRequest.ApiToken;
            var userKey = testRequest.UserKey;

            if (apiToken.IsPlaceholder() || userKey.IsPlaceholder())
            {
                var existing = await GetExistingProviderConfig<PushoverConfig>(
                    testRequest.ProviderId, NotificationProviderType.Pushover, p => p.PushoverConfiguration);

                if (existing is null)
                {
                    return BadRequest(new { Message = "Sensitive fields cannot be placeholder values" });
                }

                if (apiToken.IsPlaceholder())
                {
                    apiToken = existing.ApiToken;
                }

                if (userKey.IsPlaceholder())
                {
                    userKey = existing.UserKey;
                }
            }

            var pushoverConfig = new PushoverConfig
            {
                ApiToken = apiToken,
                UserKey = userKey,
                Devices = testRequest.Devices,
                Priority = testRequest.Priority,
                Sound = testRequest.Sound,
                Retry = testRequest.Retry,
                Expire = testRequest.Expire,
                Tags = testRequest.Tags
            };
            pushoverConfig.Validate();

            var providerDto = new NotificationProviderDto
            {
                Id = Guid.NewGuid(),
                Name = "Test Provider",
                Type = NotificationProviderType.Pushover,
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
                Configuration = pushoverConfig
            };

            await _notificationService.SendTestNotificationAsync(providerDto);
            return Ok(new { Message = "Test notification sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test Pushover provider");
            return BadRequest(new { Message = $"Test failed: {ex.Message}" });
        }
    }

    [HttpPost("gotify")]
    public async Task<IActionResult> CreateGotifyProvider([FromBody] CreateGotifyProviderRequest newProvider)
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

            if (newProvider.ApplicationToken.IsPlaceholder())
            {
                return BadRequest("Application token cannot be a placeholder value");
            }

            var gotifyConfig = new GotifyConfig
            {
                ServerUrl = newProvider.ServerUrl,
                ApplicationToken = newProvider.ApplicationToken,
                Priority = newProvider.Priority
            };
            gotifyConfig.Validate();

            var provider = new NotificationConfig
            {
                Name = newProvider.Name,
                Type = NotificationProviderType.Gotify,
                IsEnabled = newProvider.IsEnabled,
                OnFailedImportStrike = newProvider.OnFailedImportStrike,
                OnStalledStrike = newProvider.OnStalledStrike,
                OnSlowStrike = newProvider.OnSlowStrike,
                OnQueueItemDeleted = newProvider.OnQueueItemDeleted,
                OnDownloadCleaned = newProvider.OnDownloadCleaned,
                OnCategoryChanged = newProvider.OnCategoryChanged,
                GotifyConfiguration = gotifyConfig
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
            _logger.LogError(ex, "Failed to create Gotify provider");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("gotify/{id:guid}")]
    public async Task<IActionResult> UpdateGotifyProvider(Guid id, [FromBody] UpdateGotifyProviderRequest updatedProvider)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var existingProvider = await _dataContext.NotificationConfigs
                .Include(p => p.GotifyConfiguration)
                .FirstOrDefaultAsync(p => p.Id == id && p.Type == NotificationProviderType.Gotify);

            if (existingProvider == null)
            {
                return NotFound($"Gotify provider with ID {id} not found");
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

            var gotifyConfig = new GotifyConfig
            {
                ServerUrl = updatedProvider.ServerUrl,
                ApplicationToken = updatedProvider.ApplicationToken.IsPlaceholder()
                    ? existingProvider.GotifyConfiguration!.ApplicationToken
                    : updatedProvider.ApplicationToken,
                Priority = updatedProvider.Priority
            };

            if (existingProvider.GotifyConfiguration != null)
            {
                gotifyConfig = gotifyConfig with { Id = existingProvider.GotifyConfiguration.Id };
            }
            gotifyConfig.Validate();

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
                GotifyConfiguration = gotifyConfig,
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
            _logger.LogError(ex, "Failed to update Gotify provider with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPost("gotify/test")]
    public async Task<IActionResult> TestGotifyProvider([FromBody] TestGotifyProviderRequest testRequest)
    {
        try
        {
            var applicationToken = testRequest.ApplicationToken;

            if (applicationToken.IsPlaceholder())
            {
                var existing = await GetExistingProviderConfig<GotifyConfig>(
                    testRequest.ProviderId, NotificationProviderType.Gotify, p => p.GotifyConfiguration);

                if (existing is null)
                    return BadRequest(new { Message = "Application token cannot be a placeholder value" });

                applicationToken = existing.ApplicationToken;
            }

            var gotifyConfig = new GotifyConfig
            {
                ServerUrl = testRequest.ServerUrl,
                ApplicationToken = applicationToken,
                Priority = testRequest.Priority
            };
            gotifyConfig.Validate();

            var providerDto = new NotificationProviderDto
            {
                Id = Guid.NewGuid(),
                Name = "Test Provider",
                Type = NotificationProviderType.Gotify,
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
                Configuration = gotifyConfig
            };

            await _notificationService.SendTestNotificationAsync(providerDto);
            return Ok(new { Message = "Test notification sent successfully" });
        }
        catch (GotifyException ex)
        {
            _logger.LogWarning(ex, "Failed to test Gotify provider");
            return BadRequest(new { Message = $"Test failed: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test Gotify provider");
            return BadRequest(new { Message = $"Test failed: {ex.Message}" });
        }
    }

    private async Task<T?> GetExistingProviderConfig<T>(
        Guid? providerId,
        NotificationProviderType expectedType,
        Func<NotificationConfig, T?> configSelector) where T : class
    {
        if (!providerId.HasValue)
        {
            return null;
        }

        var provider = await _dataContext.NotificationConfigs
            .Include(p => p.NotifiarrConfiguration)
            .Include(p => p.AppriseConfiguration)
            .Include(p => p.NtfyConfiguration)
            .Include(p => p.PushoverConfiguration)
            .Include(p => p.TelegramConfiguration)
            .Include(p => p.DiscordConfiguration)
            .Include(p => p.GotifyConfiguration)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == providerId.Value && p.Type == expectedType);

        return provider is null ? null : configSelector(provider);
    }
}

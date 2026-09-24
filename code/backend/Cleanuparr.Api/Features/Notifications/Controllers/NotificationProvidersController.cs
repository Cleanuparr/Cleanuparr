using System.Text.Json;
using Cleanuparr.Api.Extensions;
using Cleanuparr.Api.Features.Notifications.Contracts.Requests;
using Cleanuparr.Api.Features.Notifications.Contracts.Responses;
using Cleanuparr.Api.Features.Notifications.Descriptors;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Features.Notifications.Controllers;

[ApiController]
[Route("api/configuration/notification_providers")]
[Authorize]
public sealed class NotificationProvidersController : ControllerBase
{
    private const string ProviderNameRequiredMessage = "Provider name is required";
    private const string DuplicateProviderNameMessage = "A provider with this name already exists";
    private const string SensitiveFieldsPlaceholderMessage = "Sensitive fields cannot be placeholder values";
    private const string TestNotificationSuccessMessage = "Test notification sent successfully";

    private readonly ILogger<NotificationProvidersController> _logger;
    private readonly DataContext _dataContext;
    private readonly INotificationConfigurationService _notificationConfigurationService;
    private readonly NotificationService _notificationService;
    private readonly IAppriseCliDetector _appriseCliDetector;
    private readonly TimeProvider _timeProvider;
    private readonly INotificationProviderDescriptorRegistry _descriptorRegistry;
    private readonly JsonSerializerOptions _jsonOptions;

    public NotificationProvidersController(
        ILogger<NotificationProvidersController> logger,
        DataContext dataContext,
        INotificationConfigurationService notificationConfigurationService,
        NotificationService notificationService,
        IAppriseCliDetector appriseCliDetector,
        TimeProvider timeProvider,
        INotificationProviderDescriptorRegistry descriptorRegistry,
        JsonSerializerOptions jsonOptions)
    {
        _logger = logger;
        _dataContext = dataContext;
        _notificationConfigurationService = notificationConfigurationService;
        _notificationService = notificationService;
        _appriseCliDetector = appriseCliDetector;
        _timeProvider = timeProvider;
        _descriptorRegistry = descriptorRegistry;
        _jsonOptions = jsonOptions;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotificationProviders()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            List<NotificationConfig> providers = await IncludeAllConfigurations(_dataContext.NotificationConfigs)
                .AsNoTracking()
                .ToListAsync();

            List<NotificationProviderResponse> providerDtos = providers
                .Where(p => !EnumSentinel.IsUnknown(p.Type))
                .Select(MapProvider)
                .OrderBy(x => x.Type.ToString())
                .ThenBy(x => x.Name)
                .ToList();

            NotificationProvidersResponse response = new() { Providers = providerDtos };
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

    /// <summary>
    /// Creates a notification provider of the given type.
    /// </summary>
    [HttpPost("{type}")]
    public async Task<IActionResult> CreateProvider(NotificationProviderType type, [FromBody] JsonElement requestBody)
    {
        if (!TryGetDescriptor(type, out NotificationProviderDescriptor descriptor, out IActionResult? notSupported))
        {
            return notSupported!;
        }

        CreateNotificationProviderRequestBase request =
            (CreateNotificationProviderRequestBase)requestBody.Deserialize(descriptor.CreateRequestType, _jsonOptions)!;

        await DataContext.Lock.WaitAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return this.ProblemResult(StatusCodes.Status400BadRequest, ProviderNameRequiredMessage);
            }

            int duplicateConfig = await _dataContext.NotificationConfigs.CountAsync(x => x.Name == request.Name);
            if (duplicateConfig > 0)
            {
                return this.ProblemResult(StatusCodes.Status400BadRequest, DuplicateProviderNameMessage);
            }

            foreach (NotificationProviderSensitiveField field in descriptor.SensitiveFields)
            {
                if (GetFieldValue(request, field.Name).IsPlaceholder())
                {
                    return this.ProblemResult(StatusCodes.Status400BadRequest, field.PlaceholderErrorMessage);
                }
            }

            IConfig config = descriptor.BuildConfig(request);
            config.Validate();

            NotificationConfig provider = descriptor.WithConfig(new NotificationConfig
            {
                Name = request.Name,
                Type = type,
                IsEnabled = request.IsEnabled,
                OnFailedImportStrike = request.OnFailedImportStrike,
                OnStalledStrike = request.OnStalledStrike,
                OnSlowStrike = request.OnSlowStrike,
                OnQueueItemDeleted = request.OnQueueItemDeleted,
                OnDownloadCleaned = request.OnDownloadCleaned,
                OnDownloadStopped = request.OnDownloadStopped,
                OnCategoryChanged = request.OnCategoryChanged,
                OnSearchTriggered = request.OnSearchTriggered,
                OnSearchItemGrabbed = request.OnSearchItemGrabbed,
                OnForceImported = request.OnForceImported
            }, config);

            _dataContext.NotificationConfigs.Add(provider);
            await _dataContext.SaveChangesAsync();

            await _notificationConfigurationService.InvalidateCacheAsync();

            NotificationProviderResponse providerDto = MapProvider(provider);
            return CreatedAtAction(nameof(GetNotificationProviders), new { id = provider.Id }, providerDto);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    /// <summary>
    /// Updates a notification provider of the given type.
    /// </summary>
    [HttpPut("{type}/{id:guid}")]
    public async Task<IActionResult> UpdateProvider(NotificationProviderType type, Guid id, [FromBody] JsonElement requestBody)
    {
        if (!TryGetDescriptor(type, out NotificationProviderDescriptor descriptor, out IActionResult? notSupported))
        {
            return notSupported!;
        }

        UpdateNotificationProviderRequestBase request =
            (UpdateNotificationProviderRequestBase)requestBody.Deserialize(descriptor.UpdateRequestType, _jsonOptions)!;

        await DataContext.Lock.WaitAsync();
        try
        {
            NotificationConfig? existingProvider = await IncludeAllConfigurations(_dataContext.NotificationConfigs)
                .FirstOrDefaultAsync(p => p.Id == id && p.Type == type);

            if (existingProvider is null)
            {
                return this.ProblemResult(StatusCodes.Status404NotFound, $"{type} provider with ID {id} not found");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return this.ProblemResult(StatusCodes.Status400BadRequest, ProviderNameRequiredMessage);
            }

            int duplicateConfig = await _dataContext.NotificationConfigs
                .Where(x => x.Id != id)
                .Where(x => x.Name == request.Name)
                .CountAsync();
            if (duplicateConfig > 0)
            {
                return this.ProblemResult(StatusCodes.Status400BadRequest, DuplicateProviderNameMessage);
            }

            IConfig? existingConfig = descriptor.GetConfig(existingProvider);
            IConfig newConfig = descriptor.BuildUpdateConfig(request, existingConfig);
            newConfig.Validate();

            NotificationConfig newProvider = descriptor.WithConfig(existingProvider with
            {
                Name = request.Name,
                IsEnabled = request.IsEnabled,
                OnFailedImportStrike = request.OnFailedImportStrike,
                OnStalledStrike = request.OnStalledStrike,
                OnSlowStrike = request.OnSlowStrike,
                OnQueueItemDeleted = request.OnQueueItemDeleted,
                OnDownloadCleaned = request.OnDownloadCleaned,
                OnDownloadStopped = request.OnDownloadStopped,
                OnCategoryChanged = request.OnCategoryChanged,
                OnSearchTriggered = request.OnSearchTriggered,
                OnSearchItemGrabbed = request.OnSearchItemGrabbed,
                OnForceImported = request.OnForceImported,
                UpdatedAt = _timeProvider.GetUtcNow()
            }, newConfig);

            _dataContext.NotificationConfigs.Remove(existingProvider);
            _dataContext.NotificationConfigs.Add(newProvider);

            await _dataContext.SaveChangesAsync();
            await _notificationConfigurationService.InvalidateCacheAsync();

            NotificationProviderResponse providerDto = MapProvider(newProvider);
            return Ok(providerDto);
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
            NotificationConfig? existingProvider = await IncludeAllConfigurations(_dataContext.NotificationConfigs)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (existingProvider == null)
            {
                return this.ProblemResult(StatusCodes.Status404NotFound, $"Notification provider with ID {id} not found");
            }

            _dataContext.NotificationConfigs.Remove(existingProvider);
            await _dataContext.SaveChangesAsync();

            await _notificationConfigurationService.InvalidateCacheAsync();

            _logger.LogInformation("Removed notification provider {ProviderName} with ID {ProviderId}",
                existingProvider.Name, existingProvider.Id);

            return NoContent();
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    /// <summary>
    /// Sends a test notification for the given provider type, without persisting anything.
    /// </summary>
    [HttpPost("{type}/test")]
    public async Task<IActionResult> TestProvider(NotificationProviderType type, [FromBody] JsonElement requestBody)
    {
        if (!TryGetDescriptor(type, out NotificationProviderDescriptor descriptor, out IActionResult? notSupported))
        {
            return notSupported!;
        }

        TestNotificationProviderRequestBase request =
            (TestNotificationProviderRequestBase)requestBody.Deserialize(descriptor.TestRequestType, _jsonOptions)!;

        try
        {
            bool needsExisting = descriptor.SensitiveFields.Any(field => GetFieldValue(request, field.Name).IsPlaceholder());

            IConfig? existingConfig = null;
            if (needsExisting)
            {
                existingConfig = await GetExistingProviderConfig(request.ProviderId, type);
                if (existingConfig is null)
                {
                    string message = descriptor.SensitiveFields.Count == 1
                        ? descriptor.SensitiveFields[0].PlaceholderErrorMessage
                        : SensitiveFieldsPlaceholderMessage;
                    return this.ProblemResult(StatusCodes.Status400BadRequest, message);
                }
            }

            IConfig config = descriptor.BuildTestConfig(request, existingConfig);
            config.Validate();

            NotificationProviderDto providerDto = new()
            {
                Id = Guid.NewGuid(),
                Name = "Test Provider",
                Type = type,
                IsEnabled = true,
                Events = new NotificationEventFlags
                {
                    OnFailedImportStrike = true,
                    OnStalledStrike = false,
                    OnSlowStrike = false,
                    OnQueueItemDeleted = false,
                    OnDownloadCleaned = false,
                    OnDownloadStopped = false,
                    OnCategoryChanged = false,
                    OnSearchTriggered = false,
                    OnSearchItemGrabbed = false,
                    OnForceImported = false
                },
                Configuration = config
            };

            await _notificationService.SendTestNotificationAsync(providerDto);
            return Ok(new { Message = TestNotificationSuccessMessage });
        }
        catch (Exception ex)
        {
            throw new NotificationTestException($"Test failed: {ex.Message}", ex);
        }
    }

    private NotificationProviderResponse MapProvider(NotificationConfig provider)
    {
        NotificationProviderDescriptor descriptor = _descriptorRegistry.GetDescriptor(provider.Type);

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
                OnDownloadStopped = provider.OnDownloadStopped,
                OnCategoryChanged = provider.OnCategoryChanged,
                OnSearchTriggered = provider.OnSearchTriggered,
                OnSearchItemGrabbed = provider.OnSearchItemGrabbed,
                OnForceImported = provider.OnForceImported
            },
            Configuration = (object?)descriptor.GetConfig(provider) ?? new object()
        };
    }

    private bool TryGetDescriptor(
        NotificationProviderType type,
        out NotificationProviderDescriptor descriptor,
        out IActionResult? notSupportedResult)
    {
        try
        {
            descriptor = _descriptorRegistry.GetDescriptor(type);
            notSupportedResult = null;
            return true;
        }
        catch (NotSupportedException)
        {
            descriptor = null!;
            notSupportedResult = this.ProblemResult(
                StatusCodes.Status404NotFound, $"Notification provider type {type} is not supported");
            return false;
        }
    }

    private async Task<IConfig?> GetExistingProviderConfig(Guid? providerId, NotificationProviderType type)
    {
        if (!providerId.HasValue)
        {
            return null;
        }

        NotificationConfig? provider = await IncludeAllConfigurations(_dataContext.NotificationConfigs)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == providerId.Value && p.Type == type);

        return provider is null ? null : _descriptorRegistry.GetDescriptor(type).GetConfig(provider);
    }

    private static string? GetFieldValue(object request, string propertyName) =>
        (string?)request.GetType().GetProperty(propertyName)!.GetValue(request);

    private static IQueryable<NotificationConfig> IncludeAllConfigurations(IQueryable<NotificationConfig> query) =>
        query
            .Include(p => p.NotifiarrConfiguration)
            .Include(p => p.AppriseConfiguration)
            .Include(p => p.NtfyConfiguration)
            .Include(p => p.PushoverConfiguration)
            .Include(p => p.TelegramConfiguration)
            .Include(p => p.DiscordConfiguration)
            .Include(p => p.GotifyConfiguration);
}

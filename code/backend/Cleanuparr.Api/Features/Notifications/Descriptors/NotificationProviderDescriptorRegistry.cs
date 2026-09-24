using Cleanuparr.Api.Features.Notifications.Contracts.Requests;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Notification;

namespace Cleanuparr.Api.Features.Notifications.Descriptors;

/// <summary>
/// Registry of <see cref="NotificationProviderDescriptor"/> instances, one per <see cref="NotificationProviderType"/>,
/// extracted from the 21 hand-written actions in NotificationProvidersController.
/// </summary>
public sealed class NotificationProviderDescriptorRegistry : INotificationProviderDescriptorRegistry
{
    /// <inheritdoc/>
    public NotificationProviderDescriptor GetDescriptor(NotificationProviderType type) =>
        type switch
        {
            NotificationProviderType.Notifiarr => NotifiarrDescriptor,
            NotificationProviderType.Apprise => AppriseDescriptor,
            NotificationProviderType.Ntfy => NtfyDescriptor,
            NotificationProviderType.Pushover => PushoverDescriptor,
            NotificationProviderType.Telegram => TelegramDescriptor,
            NotificationProviderType.Discord => DiscordDescriptor,
            NotificationProviderType.Gotify => GotifyDescriptor,
            _ => throw new NotSupportedException($"Provider type {type} is not supported")
        };

    private static readonly NotificationProviderDescriptor NotifiarrDescriptor = new()
    {
        Type = NotificationProviderType.Notifiarr,
        UrlSegment = "notifiarr",
        ConfigType = typeof(NotifiarrConfig),
        BuildConfig = request =>
        {
            CreateNotifiarrProviderRequest typedRequest = (CreateNotifiarrProviderRequest)request;
            return new NotifiarrConfig
            {
                ApiKey = typedRequest.ApiKey,
                ChannelId = typedRequest.ChannelId
            };
        },
        SensitiveFields =
        [
            new NotificationProviderSensitiveField
            {
                Name = nameof(NotifiarrConfig.ApiKey),
                PlaceholderErrorMessage = "API key cannot be a placeholder value"
            }
        ]
    };

    private static readonly NotificationProviderDescriptor AppriseDescriptor = new()
    {
        Type = NotificationProviderType.Apprise,
        UrlSegment = "apprise",
        ConfigType = typeof(AppriseConfig),
        BuildConfig = request =>
        {
            CreateAppriseProviderRequest typedRequest = (CreateAppriseProviderRequest)request;
            return new AppriseConfig
            {
                Mode = typedRequest.Mode,
                Url = typedRequest.Url,
                Key = typedRequest.Key,
                Tags = typedRequest.Tags,
                ServiceUrls = typedRequest.ServiceUrls
            };
        },
        SensitiveFields =
        [
            new NotificationProviderSensitiveField
            {
                Name = nameof(AppriseConfig.Key),
                PlaceholderErrorMessage = "Key cannot be a placeholder value"
            },
            new NotificationProviderSensitiveField
            {
                Name = nameof(AppriseConfig.ServiceUrls),
                PlaceholderErrorMessage = "Service URLs cannot be a placeholder value"
            }
        ]
    };

    private static readonly NotificationProviderDescriptor NtfyDescriptor = new()
    {
        Type = NotificationProviderType.Ntfy,
        UrlSegment = "ntfy",
        ConfigType = typeof(NtfyConfig),
        BuildConfig = request =>
        {
            CreateNtfyProviderRequest typedRequest = (CreateNtfyProviderRequest)request;
            return new NtfyConfig
            {
                ServerUrl = typedRequest.ServerUrl,
                Topics = typedRequest.Topics,
                AuthenticationType = typedRequest.AuthenticationType,
                Username = typedRequest.Username,
                Password = typedRequest.Password,
                AccessToken = typedRequest.AccessToken,
                Priority = typedRequest.Priority,
                Tags = typedRequest.Tags
            };
        },
        SensitiveFields =
        [
            new NotificationProviderSensitiveField
            {
                Name = nameof(NtfyConfig.Password),
                PlaceholderErrorMessage = "Password cannot be a placeholder value"
            },
            new NotificationProviderSensitiveField
            {
                Name = nameof(NtfyConfig.AccessToken),
                PlaceholderErrorMessage = "Access token cannot be a placeholder value"
            }
        ]
    };

    private static readonly NotificationProviderDescriptor PushoverDescriptor = new()
    {
        Type = NotificationProviderType.Pushover,
        UrlSegment = "pushover",
        ConfigType = typeof(PushoverConfig),
        BuildConfig = request =>
        {
            CreatePushoverProviderRequest typedRequest = (CreatePushoverProviderRequest)request;
            return new PushoverConfig
            {
                ApiToken = typedRequest.ApiToken,
                UserKey = typedRequest.UserKey,
                Devices = typedRequest.Devices,
                Priority = typedRequest.Priority,
                Sound = typedRequest.Sound,
                Retry = typedRequest.Retry,
                Expire = typedRequest.Expire,
                Tags = typedRequest.Tags
            };
        },
        SensitiveFields =
        [
            new NotificationProviderSensitiveField
            {
                Name = nameof(PushoverConfig.ApiToken),
                PlaceholderErrorMessage = "API token cannot be a placeholder value"
            },
            new NotificationProviderSensitiveField
            {
                Name = nameof(PushoverConfig.UserKey),
                PlaceholderErrorMessage = "User key cannot be a placeholder value"
            }
        ]
    };

    private static readonly NotificationProviderDescriptor TelegramDescriptor = new()
    {
        Type = NotificationProviderType.Telegram,
        UrlSegment = "telegram",
        ConfigType = typeof(TelegramConfig),
        BuildConfig = request =>
        {
            CreateTelegramProviderRequest typedRequest = (CreateTelegramProviderRequest)request;
            return new TelegramConfig
            {
                BotToken = typedRequest.BotToken,
                ChatId = typedRequest.ChatId,
                TopicId = typedRequest.TopicId,
                SendSilently = typedRequest.SendSilently
            };
        },
        SensitiveFields =
        [
            new NotificationProviderSensitiveField
            {
                Name = nameof(TelegramConfig.BotToken),
                PlaceholderErrorMessage = "Bot token cannot be a placeholder value"
            }
        ]
    };

    private static readonly NotificationProviderDescriptor DiscordDescriptor = new()
    {
        Type = NotificationProviderType.Discord,
        UrlSegment = "discord",
        ConfigType = typeof(DiscordConfig),
        BuildConfig = request =>
        {
            CreateDiscordProviderRequest typedRequest = (CreateDiscordProviderRequest)request;
            return new DiscordConfig
            {
                WebhookUrl = typedRequest.WebhookUrl,
                Username = typedRequest.Username,
                AvatarUrl = typedRequest.AvatarUrl
            };
        },
        SensitiveFields =
        [
            new NotificationProviderSensitiveField
            {
                Name = nameof(DiscordConfig.WebhookUrl),
                PlaceholderErrorMessage = "Webhook URL cannot be a placeholder value"
            }
        ]
    };

    private static readonly NotificationProviderDescriptor GotifyDescriptor = new()
    {
        Type = NotificationProviderType.Gotify,
        UrlSegment = "gotify",
        ConfigType = typeof(GotifyConfig),
        BuildConfig = request =>
        {
            CreateGotifyProviderRequest typedRequest = (CreateGotifyProviderRequest)request;
            return new GotifyConfig
            {
                ServerUrl = typedRequest.ServerUrl,
                ApplicationToken = typedRequest.ApplicationToken,
                Priority = typedRequest.Priority
            };
        },
        SensitiveFields =
        [
            new NotificationProviderSensitiveField
            {
                Name = nameof(GotifyConfig.ApplicationToken),
                PlaceholderErrorMessage = "Application token cannot be a placeholder value"
            }
        ]
    };
}

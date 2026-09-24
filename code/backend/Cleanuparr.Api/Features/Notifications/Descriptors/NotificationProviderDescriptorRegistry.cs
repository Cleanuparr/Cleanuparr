using Cleanuparr.Api.Features.Notifications.Contracts.Requests;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;

namespace Cleanuparr.Api.Features.Notifications.Descriptors;

/// <summary>
/// One <see cref="NotificationProviderDescriptor"/> per <see cref="NotificationProviderType"/>.
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
        CreateRequestType = typeof(CreateNotifiarrProviderRequest),
        UpdateRequestType = typeof(UpdateNotifiarrProviderRequest),
        TestRequestType = typeof(TestNotifiarrProviderRequest),
        BuildConfig = request =>
        {
            CreateNotifiarrProviderRequest typedRequest = (CreateNotifiarrProviderRequest)request;
            return new NotifiarrConfig
            {
                ApiKey = typedRequest.ApiKey,
                ChannelId = typedRequest.ChannelId
            };
        },
        BuildUpdateConfig = (request, existing) =>
        {
            UpdateNotifiarrProviderRequest typedRequest = (UpdateNotifiarrProviderRequest)request;
            NotifiarrConfig? existingConfig = (NotifiarrConfig?)existing;
            NotifiarrConfig config = new()
            {
                ApiKey = typedRequest.ApiKey.IsPlaceholder() ? existingConfig!.ApiKey : typedRequest.ApiKey,
                ChannelId = typedRequest.ChannelId
            };
            if (existingConfig != null)
            {
                config = config with { Id = existingConfig.Id };
            }
            return config;
        },
        BuildTestConfig = (request, existing) =>
        {
            TestNotifiarrProviderRequest typedRequest = (TestNotifiarrProviderRequest)request;
            NotifiarrConfig? existingConfig = (NotifiarrConfig?)existing;
            return new NotifiarrConfig
            {
                ApiKey = typedRequest.ApiKey.IsPlaceholder() ? existingConfig!.ApiKey : typedRequest.ApiKey,
                ChannelId = typedRequest.ChannelId
            };
        },
        GetConfig = provider => provider.NotifiarrConfiguration,
        WithConfig = (provider, config) => provider with { NotifiarrConfiguration = (NotifiarrConfig)config },
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
        CreateRequestType = typeof(CreateAppriseProviderRequest),
        UpdateRequestType = typeof(UpdateAppriseProviderRequest),
        TestRequestType = typeof(TestAppriseProviderRequest),
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
        BuildUpdateConfig = (request, existing) =>
        {
            UpdateAppriseProviderRequest typedRequest = (UpdateAppriseProviderRequest)request;
            AppriseConfig? existingConfig = (AppriseConfig?)existing;
            AppriseConfig config = new()
            {
                Mode = typedRequest.Mode,
                Url = typedRequest.Url,
                Key = typedRequest.Key.IsPlaceholder() ? existingConfig!.Key : typedRequest.Key,
                Tags = typedRequest.Tags,
                ServiceUrls = typedRequest.ServiceUrls.IsPlaceholder() ? existingConfig!.ServiceUrls : typedRequest.ServiceUrls
            };
            if (existingConfig != null)
            {
                config = config with { Id = existingConfig.Id };
            }
            return config;
        },
        BuildTestConfig = (request, existing) =>
        {
            TestAppriseProviderRequest typedRequest = (TestAppriseProviderRequest)request;
            AppriseConfig? existingConfig = (AppriseConfig?)existing;
            return new AppriseConfig
            {
                Mode = typedRequest.Mode,
                Url = typedRequest.Url,
                Key = typedRequest.Key.IsPlaceholder() ? existingConfig!.Key : typedRequest.Key,
                Tags = typedRequest.Tags,
                ServiceUrls = typedRequest.ServiceUrls.IsPlaceholder() ? existingConfig!.ServiceUrls : typedRequest.ServiceUrls
            };
        },
        GetConfig = provider => provider.AppriseConfiguration,
        WithConfig = (provider, config) => provider with { AppriseConfiguration = (AppriseConfig)config },
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
        CreateRequestType = typeof(CreateNtfyProviderRequest),
        UpdateRequestType = typeof(UpdateNtfyProviderRequest),
        TestRequestType = typeof(TestNtfyProviderRequest),
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
        BuildUpdateConfig = (request, existing) =>
        {
            UpdateNtfyProviderRequest typedRequest = (UpdateNtfyProviderRequest)request;
            NtfyConfig? existingConfig = (NtfyConfig?)existing;
            NtfyConfig config = new()
            {
                ServerUrl = typedRequest.ServerUrl,
                Topics = typedRequest.Topics,
                AuthenticationType = typedRequest.AuthenticationType,
                Username = typedRequest.Username,
                Password = typedRequest.Password.IsPlaceholder() ? existingConfig!.Password : typedRequest.Password,
                AccessToken = typedRequest.AccessToken.IsPlaceholder() ? existingConfig!.AccessToken : typedRequest.AccessToken,
                Priority = typedRequest.Priority,
                Tags = typedRequest.Tags
            };
            if (existingConfig != null)
            {
                config = config with { Id = existingConfig.Id };
            }
            return config;
        },
        BuildTestConfig = (request, existing) =>
        {
            TestNtfyProviderRequest typedRequest = (TestNtfyProviderRequest)request;
            NtfyConfig? existingConfig = (NtfyConfig?)existing;
            return new NtfyConfig
            {
                ServerUrl = typedRequest.ServerUrl,
                Topics = typedRequest.Topics,
                AuthenticationType = typedRequest.AuthenticationType,
                Username = typedRequest.Username,
                Password = typedRequest.Password.IsPlaceholder() ? existingConfig!.Password : typedRequest.Password,
                AccessToken = typedRequest.AccessToken.IsPlaceholder() ? existingConfig!.AccessToken : typedRequest.AccessToken,
                Priority = typedRequest.Priority,
                Tags = typedRequest.Tags
            };
        },
        GetConfig = provider => provider.NtfyConfiguration,
        WithConfig = (provider, config) => provider with { NtfyConfiguration = (NtfyConfig)config },
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
        CreateRequestType = typeof(CreatePushoverProviderRequest),
        UpdateRequestType = typeof(UpdatePushoverProviderRequest),
        TestRequestType = typeof(TestPushoverProviderRequest),
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
        BuildUpdateConfig = (request, existing) =>
        {
            UpdatePushoverProviderRequest typedRequest = (UpdatePushoverProviderRequest)request;
            PushoverConfig? existingConfig = (PushoverConfig?)existing;
            PushoverConfig config = new()
            {
                ApiToken = typedRequest.ApiToken.IsPlaceholder() ? existingConfig!.ApiToken : typedRequest.ApiToken,
                UserKey = typedRequest.UserKey.IsPlaceholder() ? existingConfig!.UserKey : typedRequest.UserKey,
                Devices = typedRequest.Devices,
                Priority = typedRequest.Priority,
                Sound = typedRequest.Sound,
                Retry = typedRequest.Retry,
                Expire = typedRequest.Expire,
                Tags = typedRequest.Tags
            };
            if (existingConfig != null)
            {
                config = config with { Id = existingConfig.Id };
            }
            return config;
        },
        BuildTestConfig = (request, existing) =>
        {
            TestPushoverProviderRequest typedRequest = (TestPushoverProviderRequest)request;
            PushoverConfig? existingConfig = (PushoverConfig?)existing;
            return new PushoverConfig
            {
                ApiToken = typedRequest.ApiToken.IsPlaceholder() ? existingConfig!.ApiToken : typedRequest.ApiToken,
                UserKey = typedRequest.UserKey.IsPlaceholder() ? existingConfig!.UserKey : typedRequest.UserKey,
                Devices = typedRequest.Devices,
                Priority = typedRequest.Priority,
                Sound = typedRequest.Sound,
                Retry = typedRequest.Retry,
                Expire = typedRequest.Expire,
                Tags = typedRequest.Tags
            };
        },
        GetConfig = provider => provider.PushoverConfiguration,
        WithConfig = (provider, config) => provider with { PushoverConfiguration = (PushoverConfig)config },
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
        CreateRequestType = typeof(CreateTelegramProviderRequest),
        UpdateRequestType = typeof(UpdateTelegramProviderRequest),
        TestRequestType = typeof(TestTelegramProviderRequest),
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
        BuildUpdateConfig = (request, existing) =>
        {
            UpdateTelegramProviderRequest typedRequest = (UpdateTelegramProviderRequest)request;
            TelegramConfig? existingConfig = (TelegramConfig?)existing;
            TelegramConfig config = new()
            {
                BotToken = typedRequest.BotToken.IsPlaceholder() ? existingConfig!.BotToken : typedRequest.BotToken,
                ChatId = typedRequest.ChatId,
                TopicId = typedRequest.TopicId,
                SendSilently = typedRequest.SendSilently
            };
            if (existingConfig != null)
            {
                config = config with { Id = existingConfig.Id };
            }
            return config;
        },
        BuildTestConfig = (request, existing) =>
        {
            TestTelegramProviderRequest typedRequest = (TestTelegramProviderRequest)request;
            TelegramConfig? existingConfig = (TelegramConfig?)existing;
            return new TelegramConfig
            {
                BotToken = typedRequest.BotToken.IsPlaceholder() ? existingConfig!.BotToken : typedRequest.BotToken,
                ChatId = typedRequest.ChatId,
                TopicId = typedRequest.TopicId,
                SendSilently = typedRequest.SendSilently
            };
        },
        GetConfig = provider => provider.TelegramConfiguration,
        WithConfig = (provider, config) => provider with { TelegramConfiguration = (TelegramConfig)config },
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
        CreateRequestType = typeof(CreateDiscordProviderRequest),
        UpdateRequestType = typeof(UpdateDiscordProviderRequest),
        TestRequestType = typeof(TestDiscordProviderRequest),
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
        BuildUpdateConfig = (request, existing) =>
        {
            UpdateDiscordProviderRequest typedRequest = (UpdateDiscordProviderRequest)request;
            DiscordConfig? existingConfig = (DiscordConfig?)existing;
            DiscordConfig config = new()
            {
                WebhookUrl = typedRequest.WebhookUrl.IsPlaceholder() ? existingConfig!.WebhookUrl : typedRequest.WebhookUrl,
                Username = typedRequest.Username,
                AvatarUrl = typedRequest.AvatarUrl
            };
            if (existingConfig != null)
            {
                config = config with { Id = existingConfig.Id };
            }
            return config;
        },
        BuildTestConfig = (request, existing) =>
        {
            TestDiscordProviderRequest typedRequest = (TestDiscordProviderRequest)request;
            DiscordConfig? existingConfig = (DiscordConfig?)existing;
            return new DiscordConfig
            {
                WebhookUrl = typedRequest.WebhookUrl.IsPlaceholder() ? existingConfig!.WebhookUrl : typedRequest.WebhookUrl,
                Username = typedRequest.Username,
                AvatarUrl = typedRequest.AvatarUrl
            };
        },
        GetConfig = provider => provider.DiscordConfiguration,
        WithConfig = (provider, config) => provider with { DiscordConfiguration = (DiscordConfig)config },
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
        CreateRequestType = typeof(CreateGotifyProviderRequest),
        UpdateRequestType = typeof(UpdateGotifyProviderRequest),
        TestRequestType = typeof(TestGotifyProviderRequest),
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
        BuildUpdateConfig = (request, existing) =>
        {
            UpdateGotifyProviderRequest typedRequest = (UpdateGotifyProviderRequest)request;
            GotifyConfig? existingConfig = (GotifyConfig?)existing;
            GotifyConfig config = new()
            {
                ServerUrl = typedRequest.ServerUrl,
                ApplicationToken = typedRequest.ApplicationToken.IsPlaceholder() ? existingConfig!.ApplicationToken : typedRequest.ApplicationToken,
                Priority = typedRequest.Priority
            };
            if (existingConfig != null)
            {
                config = config with { Id = existingConfig.Id };
            }
            return config;
        },
        BuildTestConfig = (request, existing) =>
        {
            TestGotifyProviderRequest typedRequest = (TestGotifyProviderRequest)request;
            GotifyConfig? existingConfig = (GotifyConfig?)existing;
            return new GotifyConfig
            {
                ServerUrl = typedRequest.ServerUrl,
                ApplicationToken = typedRequest.ApplicationToken.IsPlaceholder() ? existingConfig!.ApplicationToken : typedRequest.ApplicationToken,
                Priority = typedRequest.Priority
            };
        },
        GetConfig = provider => provider.GotifyConfiguration,
        WithConfig = (provider, config) => provider with { GotifyConfiguration = (GotifyConfig)config },
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

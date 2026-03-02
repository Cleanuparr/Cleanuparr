using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cleanuparr.Api.Json;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Dtos;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Shouldly;

namespace Cleanuparr.Api.Tests.Features.SensitiveData;

/// <summary>
/// Tests that the SensitiveDataResolver correctly masks all [SensitiveData] properties
/// during JSON serialization — this is what controls the API response output.
/// </summary>
public class SensitiveDataResolverTests
{
    private readonly JsonSerializerOptions _options;
    private const string Placeholder = SensitiveDataHelper.Placeholder;

    public SensitiveDataResolverTests()
    {
        _options = new JsonSerializerOptions
        {
            TypeInfoResolver = new SensitiveDataResolver(new DefaultJsonTypeInfoResolver()),
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    #region ArrInstance

    [Fact]
    public void ArrInstance_ApiKey_IsMasked()
    {
        var instance = new ArrInstance
        {
            Name = "Sonarr",
            Url = new Uri("http://sonarr:8989"),
            ApiKey = "super-secret-api-key-12345",
            ArrConfigId = Guid.NewGuid(),
            Version = 4
        };

        var json = JsonSerializer.Serialize(instance, _options);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("apiKey").GetString().ShouldBe(Placeholder);
    }

    [Fact]
    public void ArrInstance_NonSensitiveFields_AreVisible()
    {
        var instance = new ArrInstance
        {
            Name = "Sonarr",
            Url = new Uri("http://sonarr:8989"),
            ExternalUrl = new Uri("https://sonarr.example.com"),
            ApiKey = "super-secret-api-key-12345",
            ArrConfigId = Guid.NewGuid(),
            Version = 4
        };

        var json = JsonSerializer.Serialize(instance, _options);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("name").GetString().ShouldBe("Sonarr");
        doc.RootElement.GetProperty("url").GetString().ShouldBe("http://sonarr:8989");
        doc.RootElement.GetProperty("externalUrl").GetString().ShouldBe("https://sonarr.example.com");
    }

    [Fact]
    public void ArrInstance_NullApiKey_RemainsNull()
    {
        // ApiKey is required, but let's test with the DTO which might handle null
        var dto = new ArrInstanceDto
        {
            Name = "Sonarr",
            Url = "http://sonarr:8989",
            ApiKey = null!,
            Version = 4
        };

        var json = JsonSerializer.Serialize(dto, _options);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("apiKey").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    #endregion

    #region ArrInstanceDto

    [Fact]
    public void ArrInstanceDto_ApiKey_IsMasked()
    {
        var dto = new ArrInstanceDto
        {
            Id = Guid.NewGuid(),
            Name = "Radarr",
            Url = "http://radarr:7878",
            ApiKey = "dto-secret-api-key-67890",
            Version = 5
        };

        var json = JsonSerializer.Serialize(dto, _options);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("apiKey").GetString().ShouldBe(Placeholder);
        doc.RootElement.GetProperty("name").GetString().ShouldBe("Radarr");
        doc.RootElement.GetProperty("url").GetString().ShouldBe("http://radarr:7878");
    }

    #endregion

    #region DownloadClientConfig

    [Fact]
    public void DownloadClientConfig_Password_IsMasked()
    {
        var config = new DownloadClientConfig
        {
            Name = "qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://qbit:8080"),
            Username = "admin",
            Password = "my-secret-password",
        };

        var json = JsonSerializer.Serialize(config, _options);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("password").GetString().ShouldBe(Placeholder);
    }

    [Fact]
    public void DownloadClientConfig_Username_IsVisible()
    {
        var config = new DownloadClientConfig
        {
            Name = "qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://qbit:8080"),
            Username = "admin",
            Password = "my-secret-password",
        };

        var json = JsonSerializer.Serialize(config, _options);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("username").GetString().ShouldBe("admin");
        doc.RootElement.GetProperty("name").GetString().ShouldBe("qBittorrent");
    }

    [Fact]
    public void DownloadClientConfig_NullPassword_RemainsNull()
    {
        var config = new DownloadClientConfig
        {
            Name = "qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://qbit:8080"),
        };

        var json = JsonSerializer.Serialize(config, _options);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("password").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    #endregion

    #region NotifiarrConfig

    [Fact]
    public void NotifiarrConfig_ApiKey_IsMasked()
    {
        var config = new NotifiarrConfig
        {
            ApiKey = "notifiarr-api-key-secret",
            ChannelId = "123456789"
        };

        var json = JsonSerializer.Serialize(config, _options);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("apiKey").GetString().ShouldBe(Placeholder);
        doc.RootElement.GetProperty("channelId").GetString().ShouldBe("123456789");
    }

    #endregion

    #region DiscordConfig

    [Fact]
    public void DiscordConfig_WebhookUrl_IsMasked()
    {
        var config = new DiscordConfig
        {
            WebhookUrl = "https://discord.com/api/webhooks/123456/secret-token",
            Username = "Cleanuparr Bot",
            AvatarUrl = "https://example.com/avatar.png"
        };

        var json = JsonSerializer.Serialize(config, _options);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("webhookUrl").GetString().ShouldBe(Placeholder);
        doc.RootElement.GetProperty("username").GetString().ShouldBe("Cleanuparr Bot");
        doc.RootElement.GetProperty("avatarUrl").GetString().ShouldBe("https://example.com/avatar.png");
    }

    #endregion

    #region TelegramConfig

    [Fact]
    public void TelegramConfig_BotToken_IsMasked()
    {
        var config = new TelegramConfig
        {
            BotToken = "1234567890:ABCdefGHIjklmnoPQRstuvWXyz",
            ChatId = "-1001234567890",
            TopicId = "42",
            SendSilently = true
        };

        var json = JsonSerializer.Serialize(config, _options);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("botToken").GetString().ShouldBe(Placeholder);
        doc.RootElement.GetProperty("chatId").GetString().ShouldBe("-1001234567890");
        doc.RootElement.GetProperty("topicId").GetString().ShouldBe("42");
    }

    #endregion

    #region NtfyConfig

    [Fact]
    public void NtfyConfig_PasswordAndAccessToken_AreMasked()
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.example.com",
            Topics = ["test-topic"],
            AuthenticationType = NtfyAuthenticationType.BasicAuth,
            Username = "ntfy-user",
            Password = "ntfy-secret-password",
            AccessToken = "ntfy-access-token-secret",
        };

        var json = JsonSerializer.Serialize(config, _options);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("password").GetString().ShouldBe(Placeholder);
        doc.RootElement.GetProperty("accessToken").GetString().ShouldBe(Placeholder);
        doc.RootElement.GetProperty("serverUrl").GetString().ShouldBe("https://ntfy.example.com");
        doc.RootElement.GetProperty("username").GetString().ShouldBe("ntfy-user");
    }

    [Fact]
    public void NtfyConfig_NullPasswordAndAccessToken_RemainNull()
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.example.com",
            Topics = ["test-topic"],
            AuthenticationType = NtfyAuthenticationType.None,
        };

        var json = JsonSerializer.Serialize(config, _options);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("password").ValueKind.ShouldBe(JsonValueKind.Null);
        doc.RootElement.GetProperty("accessToken").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    #endregion

    #region PushoverConfig

    [Fact]
    public void PushoverConfig_ApiTokenAndUserKey_AreMasked()
    {
        var config = new PushoverConfig
        {
            ApiToken = "pushover-api-token-secret",
            UserKey = "pushover-user-key-secret",
            Priority = PushoverPriority.Normal,
            Devices = ["iphone", "desktop"]
        };

        var json = JsonSerializer.Serialize(config, _options);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("apiToken").GetString().ShouldBe(Placeholder);
        doc.RootElement.GetProperty("userKey").GetString().ShouldBe(Placeholder);
        doc.RootElement.GetProperty("devices").GetArrayLength().ShouldBe(2);
    }

    #endregion

    #region GotifyConfig

    [Fact]
    public void GotifyConfig_ApplicationToken_IsMasked()
    {
        var config = new GotifyConfig
        {
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "gotify-app-token-secret",
            Priority = 5
        };

        var json = JsonSerializer.Serialize(config, _options);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("applicationToken").GetString().ShouldBe(Placeholder);
        doc.RootElement.GetProperty("serverUrl").GetString().ShouldBe("https://gotify.example.com");
    }

    #endregion

    #region AppriseConfig

    [Fact]
    public void AppriseConfig_Key_IsMasked_WithFullMask()
    {
        var config = new AppriseConfig
        {
            Mode = AppriseMode.Api,
            Url = "https://apprise.example.com",
            Key = "apprise-config-key-secret",
            Tags = "urgent",
        };

        var json = JsonSerializer.Serialize(config, _options);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("key").GetString().ShouldBe(Placeholder);
        doc.RootElement.GetProperty("url").GetString().ShouldBe("https://apprise.example.com");
    }

    [Fact]
    public void AppriseConfig_ServiceUrls_IsMasked_WithAppriseUrlMask()
    {
        var config = new AppriseConfig
        {
            Mode = AppriseMode.Cli,
            ServiceUrls = "discord://webhook_id/webhook_token slack://tokenA/tokenB/tokenC"
        };

        var json = JsonSerializer.Serialize(config, _options);
        var doc = JsonDocument.Parse(json);

        var maskedUrls = doc.RootElement.GetProperty("serviceUrls").GetString();
        maskedUrls.ShouldContain("discord://••••••••");
        maskedUrls.ShouldContain("slack://••••••••");
        maskedUrls.ShouldNotContain("webhook_id");
        maskedUrls.ShouldNotContain("webhook_token");
        maskedUrls.ShouldNotContain("tokenA");
    }

    [Fact]
    public void AppriseConfig_NullServiceUrls_RemainsNull()
    {
        var config = new AppriseConfig
        {
            Mode = AppriseMode.Api,
            Url = "https://apprise.example.com",
            Key = "some-key",
        };

        var json = JsonSerializer.Serialize(config, _options);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("serviceUrls").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    #endregion

    #region Polymorphic serialization (as used in NotificationProviderResponse)

    [Fact]
    public void PolymorphicSerialization_NotifiarrConfig_StillMasked()
    {
        // The notification providers endpoint casts configs to `object`.
        // Verify that the resolver still masks when serializing as a concrete type at runtime.
        object config = new NotifiarrConfig
        {
            ApiKey = "my-secret-notifiarr-key",
            ChannelId = "987654321"
        };

        var json = JsonSerializer.Serialize(config, config.GetType(), _options);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("apiKey").GetString().ShouldBe(Placeholder);
        doc.RootElement.GetProperty("channelId").GetString().ShouldBe("987654321");
    }

    [Fact]
    public void PolymorphicSerialization_DiscordConfig_StillMasked()
    {
        object config = new DiscordConfig
        {
            WebhookUrl = "https://discord.com/api/webhooks/123/secret",
            Username = "Bot"
        };

        var json = JsonSerializer.Serialize(config, config.GetType(), _options);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("webhookUrl").GetString().ShouldBe(Placeholder);
        doc.RootElement.GetProperty("username").GetString().ShouldBe("Bot");
    }

    #endregion

    #region Edge cases

    [Fact]
    public void EmptySensitiveString_IsMasked_NotReturnedEmpty()
    {
        var config = new NotifiarrConfig
        {
            ApiKey = "",
            ChannelId = "123"
        };

        var json = JsonSerializer.Serialize(config, _options);
        var doc = JsonDocument.Parse(json);

        // Even empty strings get masked to the placeholder
        doc.RootElement.GetProperty("apiKey").GetString().ShouldBe(Placeholder);
    }

    [Fact]
    public void MultipleSensitiveFields_AllMasked()
    {
        var config = new PushoverConfig
        {
            ApiToken = "token-abc-123",
            UserKey = "user-key-xyz-789",
            Priority = PushoverPriority.High,
        };

        var json = JsonSerializer.Serialize(config, _options);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("apiToken").GetString().ShouldBe(Placeholder);
        doc.RootElement.GetProperty("userKey").GetString().ShouldBe(Placeholder);
    }

    #endregion
}

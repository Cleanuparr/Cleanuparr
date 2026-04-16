using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Infrastructure.Features.Notifications.Discord;
using Cleanuparr.Infrastructure.Features.Notifications.Gotify;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;
using Cleanuparr.Infrastructure.Features.Notifications.Ntfy;
using Cleanuparr.Infrastructure.Features.Notifications.Pushover;
using Cleanuparr.Infrastructure.Features.Notifications.Telegram;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class NotificationProviderFactoryTests
{
    private readonly IAppriseProxy _appriseProxy;
    private readonly IAppriseCliProxy _appriseCliProxy;
    private readonly INtfyProxy _ntfyProxy;
    private readonly INotifiarrProxy _notifiarrProxy;
    private readonly IPushoverProxy _pushoverProxy;
    private readonly ITelegramProxy _telegramProxy;
    private readonly IDiscordProxy _discordProxy;
    private readonly IGotifyProxy _gotifyProxy;
    private readonly IServiceProvider _serviceProvider;
    private readonly NotificationProviderFactory _factory;

    public NotificationProviderFactoryTests()
    {
        _appriseProxy = Substitute.For<IAppriseProxy>();
        _appriseCliProxy = Substitute.For<IAppriseCliProxy>();
        _ntfyProxy = Substitute.For<INtfyProxy>();
        _notifiarrProxy = Substitute.For<INotifiarrProxy>();
        _pushoverProxy = Substitute.For<IPushoverProxy>();
        _telegramProxy = Substitute.For<ITelegramProxy>();
        _discordProxy = Substitute.For<IDiscordProxy>();
        _gotifyProxy = Substitute.For<IGotifyProxy>();

        var services = new ServiceCollection();
        services.AddSingleton(_appriseProxy);
        services.AddSingleton(_appriseCliProxy);
        services.AddSingleton(_ntfyProxy);
        services.AddSingleton(_notifiarrProxy);
        services.AddSingleton(_pushoverProxy);
        services.AddSingleton(_telegramProxy);
        services.AddSingleton(_discordProxy);
        services.AddSingleton(_gotifyProxy);

        _serviceProvider = services.BuildServiceProvider();
        _factory = new NotificationProviderFactory(_serviceProvider);
    }

    #region CreateProvider Tests

    [Fact]
    public void CreateProvider_AppriseType_CreatesAppriseProvider()
    {
        // Arrange
        var config = new NotificationProviderDto
        {
            Id = Guid.NewGuid(),
            Name = "TestApprise",
            Type = NotificationProviderType.Apprise,
            IsEnabled = true,
            Configuration = new AppriseConfig
            {
                Id = Guid.NewGuid(),
                Url = "http://apprise.example.com",
                Key = "testkey"
            }
        };

        // Act
        var provider = _factory.CreateProvider(config);

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<AppriseProvider>();
        provider.Name.ShouldBe("TestApprise");
        provider.Type.ShouldBe(NotificationProviderType.Apprise);
    }

    [Fact]
    public void CreateProvider_NtfyType_CreatesNtfyProvider()
    {
        // Arrange
        var config = new NotificationProviderDto
        {
            Id = Guid.NewGuid(),
            Name = "TestNtfy",
            Type = NotificationProviderType.Ntfy,
            IsEnabled = true,
            Configuration = new NtfyConfig
            {
                Id = Guid.NewGuid(),
                ServerUrl = "http://ntfy.example.com",
                Topics = new List<string> { "test-topic" },
                AuthenticationType = NtfyAuthenticationType.None,
                Priority = NtfyPriority.Default
            }
        };

        // Act
        var provider = _factory.CreateProvider(config);

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<NtfyProvider>();
        provider.Name.ShouldBe("TestNtfy");
        provider.Type.ShouldBe(NotificationProviderType.Ntfy);
    }

    [Fact]
    public void CreateProvider_NotifiarrType_CreatesNotifiarrProvider()
    {
        // Arrange
        var config = new NotificationProviderDto
        {
            Id = Guid.NewGuid(),
            Name = "TestNotifiarr",
            Type = NotificationProviderType.Notifiarr,
            IsEnabled = true,
            Configuration = new NotifiarrConfig
            {
                Id = Guid.NewGuid(),
                ApiKey = "testapikey1234567890",
                ChannelId = "123456789012345678"
            }
        };

        // Act
        var provider = _factory.CreateProvider(config);

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<NotifiarrProvider>();
        provider.Name.ShouldBe("TestNotifiarr");
        provider.Type.ShouldBe(NotificationProviderType.Notifiarr);
    }

    [Fact]
    public void CreateProvider_PushoverType_CreatesPushoverProvider()
    {
        // Arrange
        var config = new NotificationProviderDto
        {
            Id = Guid.NewGuid(),
            Name = "TestPushover",
            Type = NotificationProviderType.Pushover,
            IsEnabled = true,
            Configuration = new PushoverConfig
            {
                Id = Guid.NewGuid(),
                ApiToken = "test-api-token",
                UserKey = "test-user-key",
                Devices = new List<string>(),
                Priority = PushoverPriority.Normal,
                Sound = "",
                Tags = new List<string>()
            }
        };

        // Act
        var provider = _factory.CreateProvider(config);

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<PushoverProvider>();
        provider.Name.ShouldBe("TestPushover");
        provider.Type.ShouldBe(NotificationProviderType.Pushover);
    }

    [Fact]
    public void CreateProvider_TelegramType_CreatesTelegramProvider()
    {
        // Arrange
        var config = new NotificationProviderDto
        {
            Id = Guid.NewGuid(),
            Name = "TestTelegram",
            Type = NotificationProviderType.Telegram,
            IsEnabled = true,
            Configuration = new TelegramConfig
            {
                Id = Guid.NewGuid(),
                BotToken = "test-bot-token",
                ChatId = "123456789",
                SendSilently = false
            }
        };

        // Act
        var provider = _factory.CreateProvider(config);

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<TelegramProvider>();
        provider.Name.ShouldBe("TestTelegram");
        provider.Type.ShouldBe(NotificationProviderType.Telegram);
    }

    [Fact]
    public void CreateProvider_DiscordType_CreatesDiscordProvider()
    {
        // Arrange
        var config = new NotificationProviderDto
        {
            Id = Guid.NewGuid(),
            Name = "TestDiscord",
            Type = NotificationProviderType.Discord,
            IsEnabled = true,
            Configuration = new DiscordConfig
            {
                Id = Guid.NewGuid(),
                WebhookUrl = "test-webhook-url",
                AvatarUrl = "test-avatar-url",
                Username = "test-username",
            }
        };

        // Act
        var provider = _factory.CreateProvider(config);

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<DiscordProvider>();
        provider.Name.ShouldBe("TestDiscord");
        provider.Type.ShouldBe(NotificationProviderType.Discord);
    }

    [Fact]
    public void CreateProvider_GotifyType_CreatesGotifyProvider()
    {
        // Arrange
        var config = new NotificationProviderDto
        {
            Id = Guid.NewGuid(),
            Name = "TestGotify",
            Type = NotificationProviderType.Gotify,
            IsEnabled = true,
            Configuration = new GotifyConfig
            {
                Id = Guid.NewGuid(),
                ServerUrl = "test-server-url",
                ApplicationToken = "test-application-token",
            }
        };

        var provider = _factory.CreateProvider(config);

        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<GotifyProvider>();
        provider.Name.ShouldBe("TestGotify");
        provider.Type.ShouldBe(NotificationProviderType.Gotify);
    }

    [Fact]
    public void CreateProvider_UnsupportedType_ThrowsNotSupportedException()
    {
        // Arrange
        var config = new NotificationProviderDto
        {
            Id = Guid.NewGuid(),
            Name = "TestUnsupported",
            Type = (NotificationProviderType)999, // Invalid type
            IsEnabled = true,
            Configuration = new object()
        };

        // Act & Assert
        var exception = Should.Throw<NotSupportedException>(() => _factory.CreateProvider(config));
        exception.Message.ShouldContain("not supported");
    }

    [Fact]
    public void CreateProvider_AppriseType_UsesCorrectProxy()
    {
        // Arrange
        var config = new NotificationProviderDto
        {
            Id = Guid.NewGuid(),
            Name = "TestApprise",
            Type = NotificationProviderType.Apprise,
            IsEnabled = true,
            Configuration = new AppriseConfig
            {
                Id = Guid.NewGuid(),
                Url = "http://apprise.example.com",
                Key = "testkey"
            }
        };

        // Act
        var provider = _factory.CreateProvider(config);

        // Assert - provider was created with the injected proxy
        provider.ShouldNotBeNull();
        // The proxy would be used when SendNotificationAsync is called
    }

    [Fact]
    public void CreateProvider_PreservesProviderName()
    {
        // Arrange
        var config = new NotificationProviderDto
        {
            Id = Guid.NewGuid(),
            Name = "My Custom Provider Name",
            Type = NotificationProviderType.Ntfy,
            IsEnabled = true,
            Configuration = new NtfyConfig
            {
                Id = Guid.NewGuid(),
                ServerUrl = "http://ntfy.example.com",
                Topics = new List<string> { "test" },
                AuthenticationType = NtfyAuthenticationType.None,
                Priority = NtfyPriority.Default
            }
        };

        // Act
        var provider = _factory.CreateProvider(config);

        // Assert
        provider.Name.ShouldBe("My Custom Provider Name");
    }

    [Fact]
    public void CreateProvider_PreservesProviderType()
    {
        // Arrange
        var configs = new[]
        {
            (Type: NotificationProviderType.Apprise, Config: (object)new AppriseConfig { Id = Guid.NewGuid(), Url = "http://test.com", Key = "key" }),
            (Type: NotificationProviderType.Ntfy, Config: (object)new NtfyConfig { Id = Guid.NewGuid(), ServerUrl = "http://test.com", Topics = new List<string> { "t" }, AuthenticationType = NtfyAuthenticationType.None, Priority = NtfyPriority.Default }),
            (Type: NotificationProviderType.Notifiarr, Config: (object)new NotifiarrConfig { Id = Guid.NewGuid(), ApiKey = "1234567890", ChannelId = "12345" }),
            (Type: NotificationProviderType.Pushover, Config: (object)new PushoverConfig { Id = Guid.NewGuid(), ApiToken = "token", UserKey = "user", Devices = new List<string>(), Priority = PushoverPriority.Normal, Sound = "", Tags = new List<string>() }),
            (Type: NotificationProviderType.Telegram, Config: (object)new TelegramConfig { Id = Guid.NewGuid(), BotToken = "token", ChatId = "123456789", SendSilently = false })
        };

        foreach (var (type, configObj) in configs)
        {
            var dto = new NotificationProviderDto
            {
                Id = Guid.NewGuid(),
                Name = $"Test-{type}",
                Type = type,
                IsEnabled = true,
                Configuration = configObj
            };

            // Act
            var provider = _factory.CreateProvider(dto);

            // Assert
            provider.Type.ShouldBe(type);
        }
    }

    #endregion

    #region Service Resolution Tests

    [Fact]
    public void CreateProvider_WhenProxyNotRegistered_ThrowsException()
    {
        // Arrange - create a service provider without the proxy
        var emptyServices = new ServiceCollection();
        var emptyServiceProvider = emptyServices.BuildServiceProvider();
        var factoryWithNoServices = new NotificationProviderFactory(emptyServiceProvider);

        var config = new NotificationProviderDto
        {
            Id = Guid.NewGuid(),
            Name = "TestApprise",
            Type = NotificationProviderType.Apprise,
            IsEnabled = true,
            Configuration = new AppriseConfig
            {
                Id = Guid.NewGuid(),
                Url = "http://test.com",
                Key = "key"
            }
        };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => factoryWithNoServices.CreateProvider(config));
    }

    #endregion
}

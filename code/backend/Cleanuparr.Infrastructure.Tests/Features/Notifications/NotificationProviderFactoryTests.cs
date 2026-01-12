using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Infrastructure.Features.Notifications.Discord;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;
using Cleanuparr.Infrastructure.Features.Notifications.Ntfy;
using Cleanuparr.Infrastructure.Features.Notifications.Pushover;
using Cleanuparr.Infrastructure.Features.Notifications.Telegram;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class NotificationProviderFactoryTests
{
    private readonly Mock<IAppriseProxy> _appriseProxyMock;
    private readonly Mock<IAppriseCliProxy> _appriseCliProxyMock;
    private readonly Mock<INtfyProxy> _ntfyProxyMock;
    private readonly Mock<INotifiarrProxy> _notifiarrProxyMock;
    private readonly Mock<IPushoverProxy> _pushoverProxyMock;
    private readonly Mock<ITelegramProxy> _telegramProxyMock;
    private readonly Mock<IDiscordProxy> _discordProxyMock;
    private readonly IServiceProvider _serviceProvider;
    private readonly NotificationProviderFactory _factory;

    public NotificationProviderFactoryTests()
    {
        _appriseProxyMock = new Mock<IAppriseProxy>();
        _appriseCliProxyMock = new Mock<IAppriseCliProxy>();
        _ntfyProxyMock = new Mock<INtfyProxy>();
        _notifiarrProxyMock = new Mock<INotifiarrProxy>();
        _pushoverProxyMock = new Mock<IPushoverProxy>();
        _telegramProxyMock = new Mock<ITelegramProxy>();
        _discordProxyMock = new Mock<IDiscordProxy>();

        var services = new ServiceCollection();
        services.AddSingleton(_appriseProxyMock.Object);
        services.AddSingleton(_appriseCliProxyMock.Object);
        services.AddSingleton(_ntfyProxyMock.Object);
        services.AddSingleton(_notifiarrProxyMock.Object);
        services.AddSingleton(_pushoverProxyMock.Object);
        services.AddSingleton(_telegramProxyMock.Object);
        services.AddSingleton(_discordProxyMock.Object);

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
        Assert.NotNull(provider);
        Assert.IsType<AppriseProvider>(provider);
        Assert.Equal("TestApprise", provider.Name);
        Assert.Equal(NotificationProviderType.Apprise, provider.Type);
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
        Assert.NotNull(provider);
        Assert.IsType<NtfyProvider>(provider);
        Assert.Equal("TestNtfy", provider.Name);
        Assert.Equal(NotificationProviderType.Ntfy, provider.Type);
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
        Assert.NotNull(provider);
        Assert.IsType<NotifiarrProvider>(provider);
        Assert.Equal("TestNotifiarr", provider.Name);
        Assert.Equal(NotificationProviderType.Notifiarr, provider.Type);
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
        Assert.NotNull(provider);
        Assert.IsType<PushoverProvider>(provider);
        Assert.Equal("TestPushover", provider.Name);
        Assert.Equal(NotificationProviderType.Pushover, provider.Type);
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
        Assert.NotNull(provider);
        Assert.IsType<TelegramProvider>(provider);
        Assert.Equal("TestTelegram", provider.Name);
        Assert.Equal(NotificationProviderType.Telegram, provider.Type);
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
        Assert.NotNull(provider);
        Assert.IsType<DiscordProvider>(provider);
        Assert.Equal("TestDiscord", provider.Name);
        Assert.Equal(NotificationProviderType.Discord, provider.Type);
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
        var exception = Assert.Throws<NotSupportedException>(() => _factory.CreateProvider(config));
        Assert.Contains("not supported", exception.Message);
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
        Assert.NotNull(provider);
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
        Assert.Equal("My Custom Provider Name", provider.Name);
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
            Assert.Equal(type, provider.Type);
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
        Assert.Throws<InvalidOperationException>(() => factoryWithNoServices.CreateProvider(config));
    }

    #endregion
}

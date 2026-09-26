using System.Text.Json;
using Cleanuparr.Api.Features.Notifications.Contracts.Requests;
using Cleanuparr.Api.Features.Notifications.Contracts.Responses;
using Cleanuparr.Api.Features.Notifications.Controllers;
using Cleanuparr.Api.Features.Notifications.Descriptors;
using Cleanuparr.Api.Json;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Features.Notifications;

public class NotificationProvidersControllerTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly NotificationProvidersController _controller;
    private readonly JsonSerializerOptions _jsonOptions;

    public NotificationProvidersControllerTests()
    {
        _dataContext = ConfigControllerTestDataFactory.CreateDataContext();

        _jsonOptions = new JsonSerializerOptions();
        CleanuparrJsonConfiguration.ConfigureApiInbound(_jsonOptions);

        INotificationConfigurationService configurationService =
            Substitute.For<INotificationConfigurationService>();

        INotificationProviderFactory providerFactory = Substitute.For<INotificationProviderFactory>();
        providerFactory.CreateProvider(Arg.Any<Infrastructure.Features.Notifications.Models.NotificationProviderDto>())
            .Returns(Substitute.For<INotificationProvider>());

        NotificationService notificationService = new(
            Substitute.For<ILogger<NotificationService>>(),
            configurationService,
            providerFactory,
            TimeProvider.System);

        _controller = new NotificationProvidersController(
            Substitute.For<ILogger<NotificationProvidersController>>(),
            _dataContext,
            configurationService,
            notificationService,
            Substitute.For<IAppriseCliDetector>(),
            TimeProvider.System,
            new NotificationProviderDescriptorRegistry(),
            _jsonOptions);

        ConfigControllerTestDataFactory.ConfigureProblemDetails(_controller);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task ShouldBeStampedNow(Guid id)
    {
        NotificationConfig stored = await _dataContext.NotificationConfigs
            .AsNoTracking()
            .FirstAsync(c => c.Id == id);

        stored.UpdatedAt.ShouldBe(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    private JsonElement ToJson<T>(T request) where T : notnull =>
        JsonSerializer.SerializeToElement(request, _jsonOptions);

    private static NotificationProviderResponse Created(IActionResult result) =>
        result.ShouldBeOfType<CreatedAtActionResult>().Value.ShouldBeOfType<NotificationProviderResponse>();

    private static NotificationProviderResponse Updated(IActionResult result) =>
        result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<NotificationProviderResponse>();

    private static ProblemDetails Problem(IActionResult result) =>
        result.ShouldBeOfType<ObjectResult>().Value.ShouldBeOfType<ProblemDetails>();

    #region Notifiarr

    [Fact]
    public async Task CreateNotifiarrProvider_PersistsTheDownloadStoppedEvent()
    {
        CreateNotifiarrProviderRequest request = new()
        {
            Name = "Notifiarr",
            ApiKey = "0123456789abcdef",
            ChannelId = "123456789",
            OnDownloadStopped = true,
        };

        NotificationProviderResponse provider = Created(
            await _controller.CreateProvider(NotificationProviderType.Notifiarr, ToJson(request)));

        provider.Type.ShouldBe(NotificationProviderType.Notifiarr);
        provider.Events.OnDownloadStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateNotifiarrProvider_ChangesTheDownloadStoppedEvent()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Notifiarr, ToJson(new CreateNotifiarrProviderRequest
        {
            Name = "Notifiarr",
            ApiKey = "0123456789abcdef",
            ChannelId = "123456789",
            OnDownloadStopped = false,
        }))).Id;

        NotificationProviderResponse provider = Updated(await _controller.UpdateProvider(NotificationProviderType.Notifiarr, id,
            ToJson(new UpdateNotifiarrProviderRequest
            {
                Name = "Notifiarr",
                ApiKey = "0123456789abcdef",
                ChannelId = "123456789",
                OnDownloadStopped = true,
            })));

        provider.Events.OnDownloadStopped.ShouldBeTrue();
        await ShouldBeStampedNow(provider.Id);
    }

    [Fact]
    public async Task CreateNotifiarrProvider_WithPlaceholderApiKey_ReturnsTheFieldSpecificMessage()
    {
        IActionResult result = await _controller.CreateProvider(NotificationProviderType.Notifiarr, ToJson(new CreateNotifiarrProviderRequest
        {
            Name = "Notifiarr",
            ApiKey = "••••••••",
            ChannelId = "123456789",
        }));

        Problem(result).Detail.ShouldBe("API key cannot be a placeholder value");
    }

    [Fact]
    public async Task UpdateNotifiarrProvider_WithPlaceholderApiKey_PreservesTheExistingKey()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Notifiarr, ToJson(new CreateNotifiarrProviderRequest
        {
            Name = "Notifiarr",
            ApiKey = "0123456789abcdef",
            ChannelId = "123456789",
        }))).Id;

        await _controller.UpdateProvider(NotificationProviderType.Notifiarr, id, ToJson(new UpdateNotifiarrProviderRequest
        {
            Name = "Notifiarr",
            ApiKey = "••••••••",
            ChannelId = "987654321",
        }));

        NotifiarrConfig stored = (await _dataContext.NotificationConfigs
            .AsNoTracking()
            .Include(p => p.NotifiarrConfiguration)
            .FirstAsync(p => p.Id == id)).NotifiarrConfiguration!;

        stored.ApiKey.ShouldBe("0123456789abcdef");
        stored.ChannelId.ShouldBe("987654321");
    }

    [Fact]
    public async Task TestNotifiarrProvider_WithPlaceholderApiKeyAndNoProviderId_ReturnsTheFieldSpecificMessage()
    {
        IActionResult result = await _controller.TestProvider(NotificationProviderType.Notifiarr, ToJson(new TestNotifiarrProviderRequest
        {
            ApiKey = "••••••••",
            ChannelId = "123456789",
        }));

        Problem(result).Detail.ShouldBe("API key cannot be a placeholder value");
    }

    [Fact]
    public async Task TestNotifiarrProvider_WithRealValues_SendsSuccessfully()
    {
        IActionResult result = await _controller.TestProvider(NotificationProviderType.Notifiarr, ToJson(new TestNotifiarrProviderRequest
        {
            ApiKey = "0123456789abcdef",
            ChannelId = "123456789",
        }));

        result.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task TestNotifiarrProvider_WithPlaceholderApiKeyAndProviderId_UsesTheStoredKey()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Notifiarr, ToJson(new CreateNotifiarrProviderRequest
        {
            Name = "Notifiarr",
            ApiKey = "0123456789abcdef",
            ChannelId = "123456789",
        }))).Id;

        IActionResult result = await _controller.TestProvider(NotificationProviderType.Notifiarr, ToJson(new TestNotifiarrProviderRequest
        {
            ApiKey = "••••••••",
            ChannelId = "123456789",
            ProviderId = id,
        }));

        result.ShouldBeOfType<OkObjectResult>();
    }

    #endregion

    #region Apprise

    [Fact]
    public async Task CreateAppriseProvider_PersistsTheDownloadStoppedEvent()
    {
        CreateAppriseProviderRequest request = new()
        {
            Name = "Apprise",
            Mode = AppriseMode.Api,
            Url = "https://apprise.example.com",
            Key = "config-key",
            OnDownloadStopped = true,
        };

        NotificationProviderResponse provider = Created(
            await _controller.CreateProvider(NotificationProviderType.Apprise, ToJson(request)));

        provider.Type.ShouldBe(NotificationProviderType.Apprise);
        provider.Events.OnDownloadStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateAppriseProvider_ChangesTheDownloadStoppedEvent()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Apprise, ToJson(new CreateAppriseProviderRequest
        {
            Name = "Apprise",
            Mode = AppriseMode.Api,
            Url = "https://apprise.example.com",
            Key = "config-key",
            OnDownloadStopped = false,
        }))).Id;

        NotificationProviderResponse provider = Updated(await _controller.UpdateProvider(NotificationProviderType.Apprise, id,
            ToJson(new UpdateAppriseProviderRequest
            {
                Name = "Apprise",
                Mode = AppriseMode.Api,
                Url = "https://apprise.example.com",
                Key = "config-key",
                OnDownloadStopped = true,
            })));

        provider.Events.OnDownloadStopped.ShouldBeTrue();
        await ShouldBeStampedNow(provider.Id);
    }

    [Fact]
    public async Task CreateAppriseProvider_WithPlaceholderKey_ReturnsTheFieldSpecificMessage()
    {
        IActionResult result = await _controller.CreateProvider(NotificationProviderType.Apprise, ToJson(new CreateAppriseProviderRequest
        {
            Name = "Apprise",
            Mode = AppriseMode.Api,
            Url = "https://apprise.example.com",
            Key = "••••••••",
        }));

        Problem(result).Detail.ShouldBe("Key cannot be a placeholder value");
    }

    [Fact]
    public async Task TestAppriseProvider_WithPlaceholderKeyAndNoProviderId_ReturnsTheGenericMessage()
    {
        IActionResult result = await _controller.TestProvider(NotificationProviderType.Apprise, ToJson(new TestAppriseProviderRequest
        {
            Mode = AppriseMode.Api,
            Url = "https://apprise.example.com",
            Key = "••••••••",
        }));

        Problem(result).Detail.ShouldBe("Sensitive fields cannot be placeholder values");
    }

    [Fact]
    public async Task UpdateAppriseProvider_WithPlaceholderKey_PreservesTheExistingKeyButUpdatesServiceUrls()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Apprise, ToJson(new CreateAppriseProviderRequest
        {
            Name = "Apprise",
            Mode = AppriseMode.Api,
            Url = "https://apprise.example.com",
            Key = "config-key",
        }))).Id;

        await _controller.UpdateProvider(NotificationProviderType.Apprise, id, ToJson(new UpdateAppriseProviderRequest
        {
            Name = "Apprise",
            Mode = AppriseMode.Api,
            Url = "https://apprise.example.com/new",
            Key = "••••••••",
        }));

        AppriseConfig stored = (await _dataContext.NotificationConfigs
            .AsNoTracking()
            .Include(p => p.AppriseConfiguration)
            .FirstAsync(p => p.Id == id)).AppriseConfiguration!;

        stored.Key.ShouldBe("config-key");
        stored.Url.ShouldBe("https://apprise.example.com/new");
    }

    #endregion

    #region Ntfy

    [Fact]
    public async Task CreateNtfyProvider_PersistsTheDownloadStoppedEvent()
    {
        CreateNtfyProviderRequest request = new()
        {
            Name = "Ntfy",
            ServerUrl = "https://ntfy.sh",
            Topics = ["cleanuparr"],
            OnDownloadStopped = true,
        };

        NotificationProviderResponse provider = Created(
            await _controller.CreateProvider(NotificationProviderType.Ntfy, ToJson(request)));

        provider.Type.ShouldBe(NotificationProviderType.Ntfy);
        provider.Events.OnDownloadStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateNtfyProvider_ChangesTheDownloadStoppedEvent()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Ntfy, ToJson(new CreateNtfyProviderRequest
        {
            Name = "Ntfy",
            ServerUrl = "https://ntfy.sh",
            Topics = ["cleanuparr"],
            OnDownloadStopped = false,
        }))).Id;

        NotificationProviderResponse provider = Updated(await _controller.UpdateProvider(NotificationProviderType.Ntfy, id,
            ToJson(new UpdateNtfyProviderRequest
            {
                Name = "Ntfy",
                ServerUrl = "https://ntfy.sh",
                Topics = ["cleanuparr"],
                OnDownloadStopped = true,
            })));

        provider.Events.OnDownloadStopped.ShouldBeTrue();
        await ShouldBeStampedNow(provider.Id);
    }

    [Fact]
    public async Task CreateNtfyProvider_WithPlaceholderPassword_ReturnsTheFieldSpecificMessage()
    {
        IActionResult result = await _controller.CreateProvider(NotificationProviderType.Ntfy, ToJson(new CreateNtfyProviderRequest
        {
            Name = "Ntfy",
            ServerUrl = "https://ntfy.sh",
            Topics = ["cleanuparr"],
            Password = "••••••••",
        }));

        Problem(result).Detail.ShouldBe("Password cannot be a placeholder value");
    }

    [Fact]
    public async Task UpdateNtfyProvider_WithPlaceholderFields_PreservesTheExistingValuesButUpdatesServerUrl()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Ntfy, ToJson(new CreateNtfyProviderRequest
        {
            Name = "Ntfy",
            ServerUrl = "https://ntfy.sh",
            Topics = ["cleanuparr"],
            Password = "password",
            AccessToken = "access-token",
        }))).Id;

        await _controller.UpdateProvider(NotificationProviderType.Ntfy, id, ToJson(new UpdateNtfyProviderRequest
        {
            Name = "Ntfy",
            ServerUrl = "https://ntfy.example.com",
            Topics = ["cleanuparr"],
            Password = "••••••••",
            AccessToken = "••••••••",
        }));

        NtfyConfig stored = (await _dataContext.NotificationConfigs
            .AsNoTracking()
            .Include(p => p.NtfyConfiguration)
            .FirstAsync(p => p.Id == id)).NtfyConfiguration!;

        stored.Password.ShouldBe("password");
        stored.AccessToken.ShouldBe("access-token");
        stored.ServerUrl.ShouldBe("https://ntfy.example.com");
    }

    [Fact]
    public async Task TestNtfyProvider_WithPlaceholderPasswordAndNoProviderId_ReturnsTheGenericMessage()
    {
        IActionResult result = await _controller.TestProvider(NotificationProviderType.Ntfy, ToJson(new TestNtfyProviderRequest
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["cleanuparr"],
            Password = "••••••••",
            AccessToken = "access-token",
        }));

        Problem(result).Detail.ShouldBe("Sensitive fields cannot be placeholder values");
    }

    [Fact]
    public async Task TestNtfyProvider_WithPlaceholderAccessTokenAndNoProviderId_ReturnsTheGenericMessage()
    {
        IActionResult result = await _controller.TestProvider(NotificationProviderType.Ntfy, ToJson(new TestNtfyProviderRequest
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["cleanuparr"],
            Password = "password",
            AccessToken = "••••••••",
        }));

        Problem(result).Detail.ShouldBe("Sensitive fields cannot be placeholder values");
    }

    [Fact]
    public async Task TestNtfyProvider_WithRealValues_SendsSuccessfully()
    {
        IActionResult result = await _controller.TestProvider(NotificationProviderType.Ntfy, ToJson(new TestNtfyProviderRequest
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["cleanuparr"],
            Password = "password",
            AccessToken = "access-token",
        }));

        result.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task TestNtfyProvider_WithPlaceholderFieldsAndProviderId_UsesTheStoredValues()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Ntfy, ToJson(new CreateNtfyProviderRequest
        {
            Name = "Ntfy",
            ServerUrl = "https://ntfy.sh",
            Topics = ["cleanuparr"],
            Password = "password",
            AccessToken = "access-token",
        }))).Id;

        IActionResult result = await _controller.TestProvider(NotificationProviderType.Ntfy, ToJson(new TestNtfyProviderRequest
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["cleanuparr"],
            Password = "••••••••",
            AccessToken = "••••••••",
            ProviderId = id,
        }));

        result.ShouldBeOfType<OkObjectResult>();
    }

    #endregion

    #region Telegram

    [Fact]
    public async Task CreateTelegramProvider_PersistsTheDownloadStoppedEvent()
    {
        CreateTelegramProviderRequest request = new()
        {
            Name = "Telegram",
            BotToken = "0123456789:token",
            ChatId = "-1001234567890",
            OnDownloadStopped = true,
        };

        NotificationProviderResponse provider = Created(
            await _controller.CreateProvider(NotificationProviderType.Telegram, ToJson(request)));

        provider.Type.ShouldBe(NotificationProviderType.Telegram);
        provider.Events.OnDownloadStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateTelegramProvider_ChangesTheDownloadStoppedEvent()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Telegram, ToJson(new CreateTelegramProviderRequest
        {
            Name = "Telegram",
            BotToken = "0123456789:token",
            ChatId = "-1001234567890",
            OnDownloadStopped = false,
        }))).Id;

        NotificationProviderResponse provider = Updated(await _controller.UpdateProvider(NotificationProviderType.Telegram, id,
            ToJson(new UpdateTelegramProviderRequest
            {
                Name = "Telegram",
                BotToken = "0123456789:token",
                ChatId = "-1001234567890",
                OnDownloadStopped = true,
            })));

        provider.Events.OnDownloadStopped.ShouldBeTrue();
        await ShouldBeStampedNow(provider.Id);
    }

    [Fact]
    public async Task TestTelegramProvider_WithPlaceholderBotTokenAndNoProviderId_ReturnsTheFieldSpecificMessage()
    {
        IActionResult result = await _controller.TestProvider(NotificationProviderType.Telegram, ToJson(new TestTelegramProviderRequest
        {
            BotToken = "••••••••",
            ChatId = "-1001234567890",
        }));

        Problem(result).Detail.ShouldBe("Bot token cannot be a placeholder value");
    }

    [Fact]
    public async Task CreateTelegramProvider_WithPlaceholderBotToken_ReturnsTheFieldSpecificMessage()
    {
        IActionResult result = await _controller.CreateProvider(NotificationProviderType.Telegram, ToJson(new CreateTelegramProviderRequest
        {
            Name = "Telegram",
            BotToken = "••••••••",
            ChatId = "-1001234567890",
        }));

        Problem(result).Detail.ShouldBe("Bot token cannot be a placeholder value");
    }

    [Fact]
    public async Task UpdateTelegramProvider_WithPlaceholderBotToken_PreservesTheExistingBotTokenButUpdatesChatId()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Telegram, ToJson(new CreateTelegramProviderRequest
        {
            Name = "Telegram",
            BotToken = "0123456789:token",
            ChatId = "-1001234567890",
        }))).Id;

        await _controller.UpdateProvider(NotificationProviderType.Telegram, id, ToJson(new UpdateTelegramProviderRequest
        {
            Name = "Telegram",
            BotToken = "••••••••",
            ChatId = "-1009876543210",
        }));

        TelegramConfig stored = (await _dataContext.NotificationConfigs
            .AsNoTracking()
            .Include(p => p.TelegramConfiguration)
            .FirstAsync(p => p.Id == id)).TelegramConfiguration!;

        stored.BotToken.ShouldBe("0123456789:token");
        stored.ChatId.ShouldBe("-1009876543210");
    }

    #endregion

    #region Discord

    [Fact]
    public async Task CreateDiscordProvider_PersistsTheDownloadStoppedEvent()
    {
        CreateDiscordProviderRequest request = new()
        {
            Name = "Discord",
            WebhookUrl = "https://discord.com/api/webhooks/1/token",
            OnDownloadStopped = true,
        };

        NotificationProviderResponse provider = Created(
            await _controller.CreateProvider(NotificationProviderType.Discord, ToJson(request)));

        provider.Type.ShouldBe(NotificationProviderType.Discord);
        provider.Events.OnDownloadStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateDiscordProvider_ChangesTheDownloadStoppedEvent()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Discord, ToJson(new CreateDiscordProviderRequest
        {
            Name = "Discord",
            WebhookUrl = "https://discord.com/api/webhooks/1/token",
            OnDownloadStopped = false,
        }))).Id;

        NotificationProviderResponse provider = Updated(await _controller.UpdateProvider(NotificationProviderType.Discord, id,
            ToJson(new UpdateDiscordProviderRequest
            {
                Name = "Discord",
                WebhookUrl = "https://discord.com/api/webhooks/1/token",
                OnDownloadStopped = true,
            })));

        provider.Events.OnDownloadStopped.ShouldBeTrue();
        await ShouldBeStampedNow(provider.Id);
    }

    [Fact]
    public async Task CreateDiscordProvider_WithPlaceholderWebhookUrl_ReturnsTheFieldSpecificMessage()
    {
        IActionResult result = await _controller.CreateProvider(NotificationProviderType.Discord, ToJson(new CreateDiscordProviderRequest
        {
            Name = "Discord",
            WebhookUrl = "••••••••",
        }));

        Problem(result).Detail.ShouldBe("Webhook URL cannot be a placeholder value");
    }

    [Fact]
    public async Task UpdateDiscordProvider_WithPlaceholderWebhookUrl_PreservesTheExistingWebhookUrl()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Discord, ToJson(new CreateDiscordProviderRequest
        {
            Name = "Discord",
            WebhookUrl = "https://discord.com/api/webhooks/1/token",
        }))).Id;

        await _controller.UpdateProvider(NotificationProviderType.Discord, id, ToJson(new UpdateDiscordProviderRequest
        {
            Name = "Discord",
            WebhookUrl = "••••••••",
        }));

        DiscordConfig stored = (await _dataContext.NotificationConfigs
            .AsNoTracking()
            .Include(p => p.DiscordConfiguration)
            .FirstAsync(p => p.Id == id)).DiscordConfiguration!;

        stored.WebhookUrl.ShouldBe("https://discord.com/api/webhooks/1/token");
    }

    [Fact]
    public async Task TestDiscordProvider_WithPlaceholderWebhookUrlAndNoProviderId_ReturnsTheFieldSpecificMessage()
    {
        IActionResult result = await _controller.TestProvider(NotificationProviderType.Discord, ToJson(new TestDiscordProviderRequest
        {
            WebhookUrl = "••••••••",
        }));

        Problem(result).Detail.ShouldBe("Webhook URL cannot be a placeholder value");
    }

    [Fact]
    public async Task TestDiscordProvider_WithRealValues_SendsSuccessfully()
    {
        IActionResult result = await _controller.TestProvider(NotificationProviderType.Discord, ToJson(new TestDiscordProviderRequest
        {
            WebhookUrl = "https://discord.com/api/webhooks/1/token",
        }));

        result.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task TestDiscordProvider_WithPlaceholderWebhookUrlAndProviderId_UsesTheStoredWebhookUrl()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Discord, ToJson(new CreateDiscordProviderRequest
        {
            Name = "Discord",
            WebhookUrl = "https://discord.com/api/webhooks/1/token",
        }))).Id;

        IActionResult result = await _controller.TestProvider(NotificationProviderType.Discord, ToJson(new TestDiscordProviderRequest
        {
            WebhookUrl = "••••••••",
            ProviderId = id,
        }));

        result.ShouldBeOfType<OkObjectResult>();
    }

    #endregion

    #region Pushover

    [Fact]
    public async Task CreatePushoverProvider_PersistsTheDownloadStoppedEvent()
    {
        CreatePushoverProviderRequest request = new()
        {
            Name = "Pushover",
            ApiToken = "api-token",
            UserKey = "user-key",
            OnDownloadStopped = true,
        };

        NotificationProviderResponse provider = Created(
            await _controller.CreateProvider(NotificationProviderType.Pushover, ToJson(request)));

        provider.Type.ShouldBe(NotificationProviderType.Pushover);
        provider.Events.OnDownloadStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdatePushoverProvider_ChangesTheDownloadStoppedEvent()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Pushover, ToJson(new CreatePushoverProviderRequest
        {
            Name = "Pushover",
            ApiToken = "api-token",
            UserKey = "user-key",
            OnDownloadStopped = false,
        }))).Id;

        NotificationProviderResponse provider = Updated(await _controller.UpdateProvider(NotificationProviderType.Pushover, id,
            ToJson(new UpdatePushoverProviderRequest
            {
                Name = "Pushover",
                ApiToken = "api-token",
                UserKey = "user-key",
                OnDownloadStopped = true,
            })));

        provider.Events.OnDownloadStopped.ShouldBeTrue();
        await ShouldBeStampedNow(provider.Id);
    }

    [Fact]
    public async Task CreatePushoverProvider_WithPlaceholderApiToken_ReturnsTheFieldSpecificMessage()
    {
        IActionResult result = await _controller.CreateProvider(NotificationProviderType.Pushover, ToJson(new CreatePushoverProviderRequest
        {
            Name = "Pushover",
            ApiToken = "••••••••",
            UserKey = "user-key",
        }));

        Problem(result).Detail.ShouldBe("API token cannot be a placeholder value");
    }

    [Fact]
    public async Task UpdatePushoverProvider_WithPlaceholderFields_PreservesTheExistingValues()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Pushover, ToJson(new CreatePushoverProviderRequest
        {
            Name = "Pushover",
            ApiToken = "api-token",
            UserKey = "user-key",
        }))).Id;

        await _controller.UpdateProvider(NotificationProviderType.Pushover, id, ToJson(new UpdatePushoverProviderRequest
        {
            Name = "Pushover",
            ApiToken = "••••••••",
            UserKey = "••••••••",
        }));

        PushoverConfig stored = (await _dataContext.NotificationConfigs
            .AsNoTracking()
            .Include(p => p.PushoverConfiguration)
            .FirstAsync(p => p.Id == id)).PushoverConfiguration!;

        stored.ApiToken.ShouldBe("api-token");
        stored.UserKey.ShouldBe("user-key");
    }

    [Fact]
    public async Task TestPushoverProvider_WithPlaceholderFieldsAndNoProviderId_ReturnsTheGenericMessage()
    {
        IActionResult result = await _controller.TestProvider(NotificationProviderType.Pushover, ToJson(new TestPushoverProviderRequest
        {
            ApiToken = "••••••••",
            UserKey = "user-key",
        }));

        Problem(result).Detail.ShouldBe("Sensitive fields cannot be placeholder values");
    }

    #endregion

    #region Gotify

    [Fact]
    public async Task CreateGotifyProvider_PersistsTheDownloadStoppedEvent()
    {
        CreateGotifyProviderRequest request = new()
        {
            Name = "Gotify",
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "app-token",
            OnDownloadStopped = true,
        };

        NotificationProviderResponse provider = Created(
            await _controller.CreateProvider(NotificationProviderType.Gotify, ToJson(request)));

        provider.Type.ShouldBe(NotificationProviderType.Gotify);
        provider.Events.OnDownloadStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateGotifyProvider_ChangesTheDownloadStoppedEvent()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Gotify, ToJson(new CreateGotifyProviderRequest
        {
            Name = "Gotify",
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "app-token",
            OnDownloadStopped = false,
        }))).Id;

        NotificationProviderResponse provider = Updated(await _controller.UpdateProvider(NotificationProviderType.Gotify, id,
            ToJson(new UpdateGotifyProviderRequest
            {
                Name = "Gotify",
                ServerUrl = "https://gotify.example.com",
                ApplicationToken = "app-token",
                OnDownloadStopped = true,
            })));

        provider.Events.OnDownloadStopped.ShouldBeTrue();
        await ShouldBeStampedNow(provider.Id);
    }

    [Fact]
    public async Task CreateGotifyProvider_WithPlaceholderApplicationToken_ReturnsTheFieldSpecificMessage()
    {
        IActionResult result = await _controller.CreateProvider(NotificationProviderType.Gotify, ToJson(new CreateGotifyProviderRequest
        {
            Name = "Gotify",
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "••••••••",
        }));

        Problem(result).Detail.ShouldBe("Application token cannot be a placeholder value");
    }

    [Fact]
    public async Task UpdateGotifyProvider_WithPlaceholderApplicationToken_PreservesTheExistingTokenButUpdatesServerUrl()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Gotify, ToJson(new CreateGotifyProviderRequest
        {
            Name = "Gotify",
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "app-token",
        }))).Id;

        await _controller.UpdateProvider(NotificationProviderType.Gotify, id, ToJson(new UpdateGotifyProviderRequest
        {
            Name = "Gotify",
            ServerUrl = "https://gotify.example.com/new",
            ApplicationToken = "••••••••",
        }));

        GotifyConfig stored = (await _dataContext.NotificationConfigs
            .AsNoTracking()
            .Include(p => p.GotifyConfiguration)
            .FirstAsync(p => p.Id == id)).GotifyConfiguration!;

        stored.ApplicationToken.ShouldBe("app-token");
        stored.ServerUrl.ShouldBe("https://gotify.example.com/new");
    }

    [Fact]
    public async Task TestGotifyProvider_WithPlaceholderApplicationTokenAndNoProviderId_ReturnsTheFieldSpecificMessage()
    {
        IActionResult result = await _controller.TestProvider(NotificationProviderType.Gotify, ToJson(new TestGotifyProviderRequest
        {
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "••••••••",
        }));

        Problem(result).Detail.ShouldBe("Application token cannot be a placeholder value");
    }

    [Fact]
    public async Task TestGotifyProvider_WithRealValues_SendsSuccessfully()
    {
        IActionResult result = await _controller.TestProvider(NotificationProviderType.Gotify, ToJson(new TestGotifyProviderRequest
        {
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "app-token",
        }));

        result.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task TestGotifyProvider_WithPlaceholderApplicationTokenAndProviderId_UsesTheStoredToken()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Gotify, ToJson(new CreateGotifyProviderRequest
        {
            Name = "Gotify",
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "app-token",
        }))).Id;

        IActionResult result = await _controller.TestProvider(NotificationProviderType.Gotify, ToJson(new TestGotifyProviderRequest
        {
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "••••••••",
            ProviderId = id,
        }));

        result.ShouldBeOfType<OkObjectResult>();
    }

    #endregion

    [Fact]
    public async Task GetNotificationProviders_ReturnsTheDownloadStoppedEvent()
    {
        await _controller.CreateProvider(NotificationProviderType.Gotify, ToJson(new CreateGotifyProviderRequest
        {
            Name = "Gotify",
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "app-token",
            OnDownloadStopped = true,
        }));

        NotificationProvidersResponse response = (await _controller.GetNotificationProviders())
            .ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<NotificationProvidersResponse>();

        response.Providers.ShouldHaveSingleItem().Events.OnDownloadStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateGotifyProvider_WithoutEvents_LeavesDownloadStoppedDisabled()
    {
        NotificationProviderResponse provider = Created(await _controller.CreateProvider(NotificationProviderType.Gotify,
            ToJson(new CreateGotifyProviderRequest
            {
                Name = "Gotify",
                ServerUrl = "https://gotify.example.com",
                ApplicationToken = "app-token",
            })));

        provider.Events.OnDownloadStopped.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateProvider_DuplicateName_ReturnsBadRequest()
    {
        await _controller.CreateProvider(NotificationProviderType.Gotify, ToJson(new CreateGotifyProviderRequest
        {
            Name = "Gotify",
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "app-token",
        }));

        IActionResult result = await _controller.CreateProvider(NotificationProviderType.Gotify, ToJson(new CreateGotifyProviderRequest
        {
            Name = "Gotify",
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "app-token-2",
        }));

        Problem(result).Detail.ShouldBe("A provider with this name already exists");
    }

    [Fact]
    public async Task CreateProvider_MissingName_ReturnsBadRequest()
    {
        IActionResult result = await _controller.CreateProvider(NotificationProviderType.Gotify, ToJson(new CreateGotifyProviderRequest
        {
            Name = "",
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "app-token",
        }));

        Problem(result).Detail.ShouldBe("Provider name is required");
    }

    [Fact]
    public async Task CreateProvider_UnsupportedType_ReturnsNotFound()
    {
        IActionResult result = await _controller.CreateProvider((NotificationProviderType)999, ToJson(new CreateGotifyProviderRequest
        {
            Name = "Gotify",
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "app-token",
        }));

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(404);
    }

    #region Invalid request bodies

    private static JsonElement NullBody() =>
        JsonSerializer.Deserialize<JsonElement>("null");

    private static JsonElement WrongTypedFieldBody() =>
        JsonSerializer.Deserialize<JsonElement>("""{"Name": 123}""");

    private static JsonElement WrongTypedProviderIdBody() =>
        JsonSerializer.Deserialize<JsonElement>("""{"ProviderId": 123}""");

    [Fact]
    public async Task CreateProvider_NullBody_ReturnsBadRequestWithoutTouchingTheDatabase()
    {
        IActionResult result = await _controller.CreateProvider(NotificationProviderType.Gotify, NullBody());

        Problem(result).Detail.ShouldBe("Invalid request body");
        (await _dataContext.NotificationConfigs.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task CreateProvider_WrongTypedField_ReturnsBadRequestWithoutTouchingTheDatabase()
    {
        IActionResult result = await _controller.CreateProvider(NotificationProviderType.Gotify, WrongTypedFieldBody());

        Problem(result).Detail.ShouldBe("Invalid request body");
        (await _dataContext.NotificationConfigs.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task UpdateProvider_NullBody_ReturnsBadRequestWithoutTouchingTheDatabase()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Gotify, ToJson(new CreateGotifyProviderRequest
        {
            Name = "Gotify",
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "app-token",
        }))).Id;

        IActionResult result = await _controller.UpdateProvider(NotificationProviderType.Gotify, id, NullBody());

        Problem(result).Detail.ShouldBe("Invalid request body");
        (await _dataContext.NotificationConfigs.AsNoTracking().FirstAsync(p => p.Id == id)).Name.ShouldBe("Gotify");
    }

    [Fact]
    public async Task UpdateProvider_WrongTypedField_ReturnsBadRequestWithoutTouchingTheDatabase()
    {
        Guid id = Created(await _controller.CreateProvider(NotificationProviderType.Gotify, ToJson(new CreateGotifyProviderRequest
        {
            Name = "Gotify",
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "app-token",
        }))).Id;

        IActionResult result = await _controller.UpdateProvider(NotificationProviderType.Gotify, id, WrongTypedFieldBody());

        Problem(result).Detail.ShouldBe("Invalid request body");
        (await _dataContext.NotificationConfigs.AsNoTracking().FirstAsync(p => p.Id == id)).Name.ShouldBe("Gotify");
    }

    [Fact]
    public async Task TestProvider_NullBody_ReturnsBadRequest()
    {
        IActionResult result = await _controller.TestProvider(NotificationProviderType.Gotify, NullBody());

        Problem(result).Detail.ShouldBe("Invalid request body");
    }

    [Fact]
    public async Task TestProvider_WrongTypedField_ReturnsBadRequest()
    {
        IActionResult result = await _controller.TestProvider(NotificationProviderType.Gotify, WrongTypedProviderIdBody());

        Problem(result).Detail.ShouldBe("Invalid request body");
    }

    #endregion
}

using Cleanuparr.Api.Features.Notifications.Contracts.Requests;
using Cleanuparr.Api.Features.Notifications.Contracts.Responses;
using Cleanuparr.Api.Features.Notifications.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Persistence;
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

    public NotificationProvidersControllerTests()
    {
        _dataContext = ConfigControllerTestDataFactory.CreateDataContext();

        INotificationConfigurationService configurationService =
            Substitute.For<INotificationConfigurationService>();

        // NotificationService is sealed; the endpoints under test never reach it.
        NotificationService notificationService = new(
            Substitute.For<ILogger<NotificationService>>(),
            configurationService,
            Substitute.For<INotificationProviderFactory>());

        _controller = new NotificationProvidersController(
            Substitute.For<ILogger<NotificationProvidersController>>(),
            _dataContext,
            configurationService,
            notificationService,
            Substitute.For<IAppriseCliDetector>());

        ConfigControllerTestDataFactory.ConfigureProblemDetails(_controller);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private static NotificationProviderResponse Created(IActionResult result) =>
        result.ShouldBeOfType<CreatedAtActionResult>().Value.ShouldBeOfType<NotificationProviderResponse>();

    private static NotificationProviderResponse Updated(IActionResult result) =>
        result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<NotificationProviderResponse>();

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

        NotificationProviderResponse provider = Created(await _controller.CreateNotifiarrProvider(request));

        provider.Type.ShouldBe(NotificationProviderType.Notifiarr);
        provider.Events.OnDownloadStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateNotifiarrProvider_ChangesTheDownloadStoppedEvent()
    {
        Guid id = Created(await _controller.CreateNotifiarrProvider(new CreateNotifiarrProviderRequest
        {
            Name = "Notifiarr",
            ApiKey = "0123456789abcdef",
            ChannelId = "123456789",
            OnDownloadStopped = false,
        })).Id;

        NotificationProviderResponse provider = Updated(await _controller.UpdateNotifiarrProvider(id,
            new UpdateNotifiarrProviderRequest
            {
                Name = "Notifiarr",
                ApiKey = "0123456789abcdef",
                ChannelId = "123456789",
                OnDownloadStopped = true,
            }));

        provider.Events.OnDownloadStopped.ShouldBeTrue();
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

        NotificationProviderResponse provider = Created(await _controller.CreateAppriseProvider(request));

        provider.Type.ShouldBe(NotificationProviderType.Apprise);
        provider.Events.OnDownloadStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateAppriseProvider_ChangesTheDownloadStoppedEvent()
    {
        Guid id = Created(await _controller.CreateAppriseProvider(new CreateAppriseProviderRequest
        {
            Name = "Apprise",
            Mode = AppriseMode.Api,
            Url = "https://apprise.example.com",
            Key = "config-key",
            OnDownloadStopped = false,
        })).Id;

        NotificationProviderResponse provider = Updated(await _controller.UpdateAppriseProvider(id,
            new UpdateAppriseProviderRequest
            {
                Name = "Apprise",
                Mode = AppriseMode.Api,
                Url = "https://apprise.example.com",
                Key = "config-key",
                OnDownloadStopped = true,
            }));

        provider.Events.OnDownloadStopped.ShouldBeTrue();
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

        NotificationProviderResponse provider = Created(await _controller.CreateNtfyProvider(request));

        provider.Type.ShouldBe(NotificationProviderType.Ntfy);
        provider.Events.OnDownloadStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateNtfyProvider_ChangesTheDownloadStoppedEvent()
    {
        Guid id = Created(await _controller.CreateNtfyProvider(new CreateNtfyProviderRequest
        {
            Name = "Ntfy",
            ServerUrl = "https://ntfy.sh",
            Topics = ["cleanuparr"],
            OnDownloadStopped = false,
        })).Id;

        NotificationProviderResponse provider = Updated(await _controller.UpdateNtfyProvider(id,
            new UpdateNtfyProviderRequest
            {
                Name = "Ntfy",
                ServerUrl = "https://ntfy.sh",
                Topics = ["cleanuparr"],
                OnDownloadStopped = true,
            }));

        provider.Events.OnDownloadStopped.ShouldBeTrue();
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

        NotificationProviderResponse provider = Created(await _controller.CreateTelegramProvider(request));

        provider.Type.ShouldBe(NotificationProviderType.Telegram);
        provider.Events.OnDownloadStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateTelegramProvider_ChangesTheDownloadStoppedEvent()
    {
        Guid id = Created(await _controller.CreateTelegramProvider(new CreateTelegramProviderRequest
        {
            Name = "Telegram",
            BotToken = "0123456789:token",
            ChatId = "-1001234567890",
            OnDownloadStopped = false,
        })).Id;

        NotificationProviderResponse provider = Updated(await _controller.UpdateTelegramProvider(id,
            new UpdateTelegramProviderRequest
            {
                Name = "Telegram",
                BotToken = "0123456789:token",
                ChatId = "-1001234567890",
                OnDownloadStopped = true,
            }));

        provider.Events.OnDownloadStopped.ShouldBeTrue();
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

        NotificationProviderResponse provider = Created(await _controller.CreateDiscordProvider(request));

        provider.Type.ShouldBe(NotificationProviderType.Discord);
        provider.Events.OnDownloadStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateDiscordProvider_ChangesTheDownloadStoppedEvent()
    {
        Guid id = Created(await _controller.CreateDiscordProvider(new CreateDiscordProviderRequest
        {
            Name = "Discord",
            WebhookUrl = "https://discord.com/api/webhooks/1/token",
            OnDownloadStopped = false,
        })).Id;

        NotificationProviderResponse provider = Updated(await _controller.UpdateDiscordProvider(id,
            new UpdateDiscordProviderRequest
            {
                Name = "Discord",
                WebhookUrl = "https://discord.com/api/webhooks/1/token",
                OnDownloadStopped = true,
            }));

        provider.Events.OnDownloadStopped.ShouldBeTrue();
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

        NotificationProviderResponse provider = Created(await _controller.CreatePushoverProvider(request));

        provider.Type.ShouldBe(NotificationProviderType.Pushover);
        provider.Events.OnDownloadStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdatePushoverProvider_ChangesTheDownloadStoppedEvent()
    {
        Guid id = Created(await _controller.CreatePushoverProvider(new CreatePushoverProviderRequest
        {
            Name = "Pushover",
            ApiToken = "api-token",
            UserKey = "user-key",
            OnDownloadStopped = false,
        })).Id;

        NotificationProviderResponse provider = Updated(await _controller.UpdatePushoverProvider(id,
            new UpdatePushoverProviderRequest
            {
                Name = "Pushover",
                ApiToken = "api-token",
                UserKey = "user-key",
                OnDownloadStopped = true,
            }));

        provider.Events.OnDownloadStopped.ShouldBeTrue();
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

        NotificationProviderResponse provider = Created(await _controller.CreateGotifyProvider(request));

        provider.Type.ShouldBe(NotificationProviderType.Gotify);
        provider.Events.OnDownloadStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateGotifyProvider_ChangesTheDownloadStoppedEvent()
    {
        Guid id = Created(await _controller.CreateGotifyProvider(new CreateGotifyProviderRequest
        {
            Name = "Gotify",
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "app-token",
            OnDownloadStopped = false,
        })).Id;

        NotificationProviderResponse provider = Updated(await _controller.UpdateGotifyProvider(id,
            new UpdateGotifyProviderRequest
            {
                Name = "Gotify",
                ServerUrl = "https://gotify.example.com",
                ApplicationToken = "app-token",
                OnDownloadStopped = true,
            }));

        provider.Events.OnDownloadStopped.ShouldBeTrue();
    }

    #endregion

    [Fact]
    public async Task GetNotificationProviders_ReturnsTheDownloadStoppedEvent()
    {
        await _controller.CreateGotifyProvider(new CreateGotifyProviderRequest
        {
            Name = "Gotify",
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "app-token",
            OnDownloadStopped = true,
        });

        NotificationProvidersResponse response = (await _controller.GetNotificationProviders())
            .ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<NotificationProvidersResponse>();

        response.Providers.ShouldHaveSingleItem().Events.OnDownloadStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateGotifyProvider_WithoutEvents_LeavesDownloadStoppedDisabled()
    {
        NotificationProviderResponse provider = Created(await _controller.CreateGotifyProvider(
            new CreateGotifyProviderRequest
            {
                Name = "Gotify",
                ServerUrl = "https://gotify.example.com",
                ApplicationToken = "app-token",
            }));

        provider.Events.OnDownloadStopped.ShouldBeFalse();
    }
}

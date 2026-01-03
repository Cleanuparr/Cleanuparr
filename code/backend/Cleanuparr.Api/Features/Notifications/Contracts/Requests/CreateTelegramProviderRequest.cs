namespace Cleanuparr.Api.Features.Notifications.Contracts.Requests;

public sealed record CreateTelegramProviderRequest : CreateNotificationProviderRequestBase
{
    public string BotToken { get; init; } = string.Empty;

    public string ChatId { get; init; } = string.Empty;

    public string? TopicId { get; init; }

    public bool SendSilently { get; init; }
}

namespace Cleanuparr.Api.Features.Notifications.Contracts.Requests;

public sealed record TestTelegramProviderRequest
{
    public string BotToken { get; init; } = string.Empty;

    public string ChatId { get; init; } = string.Empty;

    public string? TopicId { get; init; }

    public bool SendSilently { get; init; }

    public Guid? ProviderId { get; init; }
}

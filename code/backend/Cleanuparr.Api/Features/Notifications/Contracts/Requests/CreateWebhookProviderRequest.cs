using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Notifications.Contracts.Requests;

public record CreateWebhookProviderRequest : CreateNotificationProviderRequestBase
{
    public string Url { get; init; } = string.Empty;

    public WebhookMethod Method { get; init; } = WebhookMethod.Post;

    public List<string> Headers { get; init; } = [];
}

using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Notifications.Contracts.Requests;

public record CreateAppriseProviderRequest : CreateNotificationProviderRequestBase
{
    public AppriseMode Mode { get; init; } = AppriseMode.Api;

    // API mode fields
    public string Url { get; init; } = string.Empty;

    public string Key { get; init; } = string.Empty;

    public string Tags { get; init; } = string.Empty;

    // CLI mode fields
    public string? ServiceUrls { get; init; }
}

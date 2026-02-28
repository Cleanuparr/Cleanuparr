using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Notifications.Contracts.Requests;

public record TestPushoverProviderRequest
{
    public string ApiToken { get; init; } = string.Empty;

    public string UserKey { get; init; } = string.Empty;

    public List<string> Devices { get; init; } = [];

    public PushoverPriority Priority { get; init; } = PushoverPriority.Normal;

    public string? Sound { get; init; }

    public int? Retry { get; init; }

    public int? Expire { get; init; }

    public List<string> Tags { get; init; } = [];

    public Guid? ProviderId { get; init; }
}

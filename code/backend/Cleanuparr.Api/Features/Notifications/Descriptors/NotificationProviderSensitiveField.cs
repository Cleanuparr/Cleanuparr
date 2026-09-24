namespace Cleanuparr.Api.Features.Notifications.Descriptors;

/// <summary>
/// A field on a notification provider that is rejected when it carries a placeholder value.
/// </summary>
public sealed record NotificationProviderSensitiveField
{
    /// <summary>
    /// The property name on the provider's request/config (e.g. "ApiKey", "ServiceUrls").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The Create-action validation error for this field. Test collapses multi-field providers
    /// into one generic message instead of reusing this text per field.
    /// </summary>
    public required string PlaceholderErrorMessage { get; init; }
}

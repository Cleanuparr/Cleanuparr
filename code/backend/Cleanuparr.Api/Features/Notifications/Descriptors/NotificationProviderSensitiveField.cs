namespace Cleanuparr.Api.Features.Notifications.Descriptors;

/// <summary>
/// Describes a single field on a notification provider that is rejected when it carries a placeholder value.
/// The message here matches the Create-action wording, which is the most granular of the three existing
/// actions - the Test action collapses multi-field providers into one generic message instead of reusing
/// this text per field. See <see cref="NotificationProviderDescriptor"/> for details.
/// </summary>
public sealed record NotificationProviderSensitiveField
{
    /// <summary>
    /// The property name on the provider's request/config (e.g. "ApiKey", "ServiceUrls").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The validation error returned by the existing Create action when this field is a placeholder.
    /// </summary>
    public required string PlaceholderErrorMessage { get; init; }
}

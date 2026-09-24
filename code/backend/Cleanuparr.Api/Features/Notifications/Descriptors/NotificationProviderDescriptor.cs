using Cleanuparr.Api.Features.Notifications.Contracts.Requests;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Notification;

namespace Cleanuparr.Api.Features.Notifications.Descriptors;

/// <summary>
/// Static, per-provider metadata that a future rewrite of NotificationProvidersController can dispatch on
/// instead of hand-writing a Create/Update/Test action per provider. Built from the 21 existing controller
/// actions - see <see cref="NotificationProviderDescriptorRegistry"/> for the per-provider values and the
/// quirks found while extracting them.
/// </summary>
/// <remarks>
/// This only models the Create-request mapping. The Update and Test actions additionally merge placeholder
/// sensitive fields with an existing <see cref="NotificationConfig"/> loaded from the database - that merge
/// logic is per-action, not per-provider, and is intentionally left out of this prep work.
/// </remarks>
public sealed record NotificationProviderDescriptor
{
    /// <summary>
    /// The provider type this descriptor describes.
    /// </summary>
    public required NotificationProviderType Type { get; init; }

    /// <summary>
    /// The route segment used under "api/configuration/notification_providers" (e.g. "notifiarr", "apprise").
    /// </summary>
    public required string UrlSegment { get; init; }

    /// <summary>
    /// The concrete <see cref="IConfig"/> type this provider persists (e.g. <see cref="NotifiarrConfig"/>).
    /// </summary>
    public required Type ConfigType { get; init; }

    /// <summary>
    /// Maps a <see cref="CreateNotificationProviderRequestBase"/> subtype to the provider's config object,
    /// the way each existing CreateXProvider action does it.
    /// </summary>
    public required Func<CreateNotificationProviderRequestBase, IConfig> BuildConfig { get; init; }

    /// <summary>
    /// The fields that the existing Create action rejects when they carry a placeholder value. Every
    /// provider has at least one; Apprise, Ntfy and Pushover have two.
    /// </summary>
    public required IReadOnlyList<NotificationProviderSensitiveField> SensitiveFields { get; init; }
}

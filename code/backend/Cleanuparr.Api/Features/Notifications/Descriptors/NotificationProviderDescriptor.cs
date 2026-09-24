using Cleanuparr.Api.Features.Notifications.Contracts.Requests;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Notification;

namespace Cleanuparr.Api.Features.Notifications.Descriptors;

/// <summary>
/// Per-provider metadata a future rewrite of NotificationProvidersController can dispatch on.
/// </summary>
/// <remarks>
/// Only models the Create-request mapping. Update and Test also merge sensitive fields against
/// an existing loaded config, which is per-action logic and left out of this prep work.
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
    /// Maps a <see cref="CreateNotificationProviderRequestBase"/> subtype to the provider's config object.
    /// </summary>
    public required Func<CreateNotificationProviderRequestBase, IConfig> BuildConfig { get; init; }

    /// <summary>
    /// Fields rejected when they carry a placeholder value.
    /// </summary>
    public required IReadOnlyList<NotificationProviderSensitiveField> SensitiveFields { get; init; }
}

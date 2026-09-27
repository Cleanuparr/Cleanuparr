using Cleanuparr.Api.Features.Notifications.Contracts.Requests;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Notification;

namespace Cleanuparr.Api.Features.Notifications.Descriptors;

/// <summary>
/// Per-provider metadata <see cref="Controllers.NotificationProvidersController"/> dispatches on.
/// </summary>
public sealed record NotificationProviderDescriptor
{
    /// <summary>
    /// The provider type this descriptor describes.
    /// </summary>
    public required NotificationProviderType Type { get; init; }

    /// <summary>
    /// The concrete <see cref="IConfig"/> type this provider persists (e.g. <see cref="NotifiarrConfig"/>).
    /// </summary>
    public required Type ConfigType { get; init; }

    /// <summary>
    /// The concrete <see cref="CreateNotificationProviderRequestBase"/> subtype the create body deserializes into.
    /// </summary>
    public required Type CreateRequestType { get; init; }

    /// <summary>
    /// The concrete <see cref="UpdateNotificationProviderRequestBase"/> subtype the update body deserializes into.
    /// </summary>
    public required Type UpdateRequestType { get; init; }

    /// <summary>
    /// The concrete <see cref="TestNotificationProviderRequestBase"/> subtype the test body deserializes into.
    /// </summary>
    public required Type TestRequestType { get; init; }

    /// <summary>
    /// Maps a <see cref="CreateNotificationProviderRequestBase"/> subtype to the provider's config object.
    /// </summary>
    public required Func<CreateNotificationProviderRequestBase, IConfig> BuildConfig { get; init; }

    /// <summary>
    /// Maps an <see cref="UpdateNotificationProviderRequestBase"/> subtype to the provider's config object,
    /// substituting the existing (DB-loaded) value for any sensitive field that carries a placeholder, and
    /// preserving the existing config row's Id.
    /// </summary>
    public required Func<UpdateNotificationProviderRequestBase, IConfig?, IConfig> BuildUpdateConfig { get; init; }

    /// <summary>
    /// Maps a <see cref="TestNotificationProviderRequestBase"/> subtype to a transient config object,
    /// substituting the existing (DB-loaded) value for any sensitive field that carries a placeholder.
    /// </summary>
    public required Func<TestNotificationProviderRequestBase, IConfig?, IConfig> BuildTestConfig { get; init; }

    /// <summary>
    /// Reads this provider's nested config navigation property off a persisted <see cref="NotificationConfig"/>.
    /// </summary>
    public required Func<NotificationConfig, IConfig?> GetConfig { get; init; }

    /// <summary>
    /// Returns a copy of the given <see cref="NotificationConfig"/> with this provider's nested config
    /// navigation property set to the given config object.
    /// </summary>
    public required Func<NotificationConfig, IConfig, NotificationConfig> WithConfig { get; init; }

    /// <summary>
    /// Fields rejected when they carry a placeholder value.
    /// </summary>
    public required IReadOnlyList<NotificationProviderSensitiveField> SensitiveFields { get; init; }
}

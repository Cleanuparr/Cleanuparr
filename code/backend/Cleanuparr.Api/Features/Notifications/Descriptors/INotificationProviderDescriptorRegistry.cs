using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Notifications.Descriptors;

/// <summary>
/// Looks up the <see cref="NotificationProviderDescriptor"/> for a given <see cref="NotificationProviderType"/>.
/// </summary>
public interface INotificationProviderDescriptorRegistry
{
    /// <summary>
    /// Returns the descriptor for the given provider type.
    /// </summary>
    /// <param name="type">The provider type to look up.</param>
    /// <exception cref="NotSupportedException">The provider type has no registered descriptor.</exception>
    NotificationProviderDescriptor GetDescriptor(NotificationProviderType type);
}

using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.Notifications.Models;

public sealed record NotificationMessage(NotificationEventType EventType, NotificationContext Context);

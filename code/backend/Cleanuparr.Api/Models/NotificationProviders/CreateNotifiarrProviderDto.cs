using System;

namespace Cleanuparr.Api.Models.NotificationProviders;

using CreateNotifiarrProviderRequest = Cleanuparr.Api.Features.Notifications.Contracts.Requests.CreateNotifiarrProviderRequest;

[Obsolete("Use Cleanuparr.Api.Features.Notifications.Contracts.Requests.CreateNotifiarrProviderRequest instead.")]
public sealed record CreateNotifiarrProviderDto : CreateNotifiarrProviderRequest;

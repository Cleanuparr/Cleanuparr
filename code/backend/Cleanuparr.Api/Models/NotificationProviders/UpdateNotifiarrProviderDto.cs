using System;

namespace Cleanuparr.Api.Models.NotificationProviders;

using UpdateNotifiarrProviderRequest = Cleanuparr.Api.Features.Notifications.Contracts.Requests.UpdateNotifiarrProviderRequest;

[Obsolete("Use Cleanuparr.Api.Features.Notifications.Contracts.Requests.UpdateNotifiarrProviderRequest instead.")]
public sealed record UpdateNotifiarrProviderDto : UpdateNotifiarrProviderRequest;

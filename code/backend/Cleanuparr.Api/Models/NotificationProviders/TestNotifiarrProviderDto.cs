using System;

namespace Cleanuparr.Api.Models.NotificationProviders;

using TestNotifiarrProviderRequest = Cleanuparr.Api.Features.Notifications.Contracts.Requests.TestNotifiarrProviderRequest;

[Obsolete("Use Cleanuparr.Api.Features.Notifications.Contracts.Requests.TestNotifiarrProviderRequest instead.")]
public sealed record TestNotifiarrProviderDto : TestNotifiarrProviderRequest;

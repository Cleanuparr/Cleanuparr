using System;

namespace Cleanuparr.Api.Models.NotificationProviders;

using UpdateAppriseProviderRequest = Cleanuparr.Api.Features.Notifications.Contracts.Requests.UpdateAppriseProviderRequest;

[Obsolete("Use Cleanuparr.Api.Features.Notifications.Contracts.Requests.UpdateAppriseProviderRequest instead.")]
public sealed record UpdateAppriseProviderDto : UpdateAppriseProviderRequest;

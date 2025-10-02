using System;

namespace Cleanuparr.Api.Models.NotificationProviders;

using CreateAppriseProviderRequest = Cleanuparr.Api.Features.Notifications.Contracts.Requests.CreateAppriseProviderRequest;

[Obsolete("Use Cleanuparr.Api.Features.Notifications.Contracts.Requests.CreateAppriseProviderRequest instead.")]
public sealed record CreateAppriseProviderDto : CreateAppriseProviderRequest;

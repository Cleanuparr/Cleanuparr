using System;

namespace Cleanuparr.Api.Models.NotificationProviders;

using TestAppriseProviderRequest = Cleanuparr.Api.Features.Notifications.Contracts.Requests.TestAppriseProviderRequest;

[Obsolete("Use Cleanuparr.Api.Features.Notifications.Contracts.Requests.TestAppriseProviderRequest instead.")]
public sealed record TestAppriseProviderDto : TestAppriseProviderRequest;

using System;

namespace Cleanuparr.Api.Models.NotificationProviders;

using TestNtfyProviderRequest = Cleanuparr.Api.Features.Notifications.Contracts.Requests.TestNtfyProviderRequest;

[Obsolete("Use Cleanuparr.Api.Features.Notifications.Contracts.Requests.TestNtfyProviderRequest instead.")]
public sealed record TestNtfyProviderDto : TestNtfyProviderRequest;

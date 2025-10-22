using System;

namespace Cleanuparr.Api.Models.NotificationProviders;

using UpdateNtfyProviderRequest = Cleanuparr.Api.Features.Notifications.Contracts.Requests.UpdateNtfyProviderRequest;

[Obsolete("Use Cleanuparr.Api.Features.Notifications.Contracts.Requests.UpdateNtfyProviderRequest instead.")]
public sealed record UpdateNtfyProviderDto : UpdateNtfyProviderRequest;

using System;

namespace Cleanuparr.Api.Models.NotificationProviders;

using CreateNtfyProviderRequest = Cleanuparr.Api.Features.Notifications.Contracts.Requests.CreateNtfyProviderRequest;

[Obsolete("Use Cleanuparr.Api.Features.Notifications.Contracts.Requests.CreateNtfyProviderRequest instead.")]
public sealed record CreateNtfyProviderDto : CreateNtfyProviderRequest;

using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Models.NotificationProviders;

public sealed record CreateNtfyProviderDto : CreateNotificationProviderBaseDto
{
    public string ServerUrl { get; init; } = string.Empty;
    
    public string Topics { get; init; } = string.Empty;
    
    public NtfyAuthenticationType AuthenticationType { get; init; } = NtfyAuthenticationType.None;
    
    public string Username { get; init; } = string.Empty;
    
    public string Password { get; init; } = string.Empty;
    
    public string AccessToken { get; init; } = string.Empty;
    
    public NtfyPriority Priority { get; init; } = NtfyPriority.Default;
    
    public string Tags { get; init; } = string.Empty;
}

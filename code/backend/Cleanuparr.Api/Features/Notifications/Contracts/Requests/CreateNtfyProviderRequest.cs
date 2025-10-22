using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Notifications.Contracts.Requests;

public record CreateNtfyProviderRequest : CreateNotificationProviderRequestBase
{
    public string ServerUrl { get; init; } = string.Empty;
    
    public List<string> Topics { get; init; } = [];
    
    public NtfyAuthenticationType AuthenticationType { get; init; } = NtfyAuthenticationType.None;
    
    public string Username { get; init; } = string.Empty;
    
    public string Password { get; init; } = string.Empty;
    
    public string AccessToken { get; init; } = string.Empty;
    
    public NtfyPriority Priority { get; init; } = NtfyPriority.Default;
    
    public List<string> Tags { get; init; } = [];
}

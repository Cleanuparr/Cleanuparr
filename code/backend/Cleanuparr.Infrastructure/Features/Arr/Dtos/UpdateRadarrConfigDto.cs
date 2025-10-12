namespace Cleanuparr.Infrastructure.Features.Arr.Dtos;

/// <summary>
/// DTO for updating Radarr configuration basic settings (instances managed separately)
/// </summary>
public record UpdateRadarrConfigDto
{
    public short FailedImportMaxStrikes { get; init; } = -1;
} 
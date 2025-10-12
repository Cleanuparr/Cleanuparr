namespace Cleanuparr.Infrastructure.Features.Arr.Dtos;

/// <summary>
/// DTO for updating Readarr configuration basic settings (instances managed separately)
/// </summary>
public record UpdateReadarrConfigDto
{
    public short FailedImportMaxStrikes { get; init; } = -1;
} 
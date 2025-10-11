namespace Cleanuparr.Infrastructure.Features.Arr.Dtos;

/// <summary>
/// DTO for updating Whisparr configuration basic settings (instances managed separately)
/// </summary>
public record UpdateWhisparrConfigDto
{
    public short FailedImportMaxStrikes { get; init; } = -1;
} 
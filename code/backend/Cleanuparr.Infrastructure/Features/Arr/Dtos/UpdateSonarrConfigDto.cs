using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Infrastructure.Features.Arr.Dtos;

/// <summary>
/// DTO for updating Sonarr configuration basic settings (instances managed separately)
/// </summary>
public record UpdateSonarrConfigDto
{
    public short FailedImportMaxStrikes { get; init; } = -1;
}

/// <summary>
/// DTO for Arr instances that can handle both existing (with ID) and new (without ID) instances
/// </summary>
public record ArrInstanceDto
{
    /// <summary>
    /// ID for existing instances, null for new instances
    /// </summary>
    public Guid? Id { get; init; }
    
    public bool Enabled { get; init; } = true;
    
    [Required]
    public required string Name { get; init; }
    
    [Required]
    public required string Url { get; init; }
    
    [Required]
    public required string ApiKey { get; init; }
} 
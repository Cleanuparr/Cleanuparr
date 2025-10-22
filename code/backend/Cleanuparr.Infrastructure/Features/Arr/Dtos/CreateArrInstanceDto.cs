using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Infrastructure.Features.Arr.Dtos;

/// <summary>
/// DTO for creating new Arr instances without requiring an ID
/// </summary>
public record CreateArrInstanceDto
{
    public bool Enabled { get; init; } = true;
    
    [Required]
    public required string Name { get; init; }
    
    [Required]
    public required string Url { get; init; }
    
    [Required]
    public required string ApiKey { get; init; }
} 
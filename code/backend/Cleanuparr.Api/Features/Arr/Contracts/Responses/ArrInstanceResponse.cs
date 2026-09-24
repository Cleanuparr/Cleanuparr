using System.ComponentModel.DataAnnotations;
using Cleanuparr.Shared.Attributes;

namespace Cleanuparr.Api.Features.Arr.Contracts.Responses;

/// <summary>
/// Response shape for Arr instances, covering both existing (with ID) and new (without ID) instances.
/// </summary>
public sealed record ArrInstanceResponse
{
    /// <summary>
    /// ID for existing instances, null for new instances
    /// </summary>
    public Guid? Id { get; init; }

    public bool Enabled { get; init; } = true;

    public float Version { get; init; }

    [Required]
    public required string Name { get; init; }

    [Required]
    public required string Url { get; init; }

    [Required]
    [SensitiveData]
    public required string ApiKey { get; init; }

    public string? ExternalUrl { get; init; }
}

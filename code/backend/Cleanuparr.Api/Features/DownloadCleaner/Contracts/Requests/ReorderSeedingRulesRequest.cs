using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;

public record ReorderSeedingRulesRequest
{
    /// <summary>
    /// IDs of seeding rules in the desired priority order (first = highest priority).
    /// </summary>
    [Required]
    public List<Guid> OrderedIds { get; init; } = [];
}

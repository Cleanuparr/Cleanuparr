using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Persistence.Models.Events;

/// <summary>
/// Stores structured data for SearchTriggered events.
/// One record per searched item.
/// </summary>
public class SearchEventData
{
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid AppEventId { get; set; }

    [JsonIgnore]
    public AppEvent AppEvent { get; set; } = null!;

    [MaxLength(500)]
    public string ItemTitle { get; set; } = string.Empty;

    public SeekerSearchType SearchType { get; set; }

    public SeekerSearchReason? SearchReason { get; set; }

    /// <summary>
    /// Titles of items grabbed after search completion, populated by SeekerCommandMonitor.
    /// </summary>
    public List<string> GrabbedItems { get; set; } = [];
}

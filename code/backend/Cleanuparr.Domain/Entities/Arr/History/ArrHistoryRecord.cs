namespace Cleanuparr.Domain.Entities.Arr.History;

/// <summary>
/// One thing an arr did with a download.
/// </summary>
/// <param name="EventType">The arr's own name for it, such as grabbed or downloadFolderImported.</param>
public sealed record ArrHistoryRecord(string? EventType);

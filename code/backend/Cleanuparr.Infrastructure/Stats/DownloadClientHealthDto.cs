namespace Cleanuparr.Infrastructure.Stats;

public class DownloadClientHealthDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public DateTimeOffset LastChecked { get; set; }
    public double? ResponseTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
}

namespace Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;

public class Text
{
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<Field> Fields { get; set; } = new List<Field>();
    public string Footer { get; set; } = string.Empty;
}

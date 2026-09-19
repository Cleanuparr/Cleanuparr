namespace Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;

public class NotifiarrDiscord
{
    public string Color { get; set; } = string.Empty;
    public Ping Ping { get; set; }
    public Images Images { get; set; }
    public Text Text { get; set; }
    public Ids Ids { get; set; }
}

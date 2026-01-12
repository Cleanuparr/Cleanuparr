namespace Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;

public class NotifiarrPayload
{
    public NotifiarrNotification Notification { get; set; } = new NotifiarrNotification();
    public NotifiarrDiscord Discord { get; set; }
}

public class NotifiarrNotification
{
    public bool Update { get; set; }
    public string Name => "Cleanuparr";
    public int? Event { get; set; }
}

public class NotifiarrDiscord
{
    public string Color { get; set; } = string.Empty;
    public Ping Ping { get; set; }
    public Images Images { get; set; }
    public Text Text { get; set; }
    public Ids Ids { get; set; }
}

public class Ping
{
    public string PingUser { get; set; }
    public string PingRole { get; set; }
}

public class Images
{
    public Uri? Thumbnail { get; set; }
    public Uri? Image { get; set; }
}

public class Text
{
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<Field> Fields { get; set; } = new List<Field>();
    public string Footer { get; set; } = string.Empty;
}

public class Field
{
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool Inline { get; set; }
}

public class Ids
{
    public string Channel { get; set; }
}
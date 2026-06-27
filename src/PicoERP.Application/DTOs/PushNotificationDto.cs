namespace PicoERP.Application.DTOs;

public class PushSubscriptionDto
{
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public string? UserId { get; set; }
}

public class PushNotificationDto
{
    public string Title { get; set; } = "پیکو ERP";
    public string Body { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Tag { get; set; }
    /// <summary>Optional: only send to this userId. Null = broadcast to all.</summary>
    public string? UserId { get; set; }
}

using PicoERP.Domain.Common;

namespace PicoERP.Domain.Entities;

/// <summary>
/// A log entry for every SMS sent or received through IPPanel.
/// Used to populate the SMS tab in ContactProfile and the global SMS page.
/// </summary>
public class SmsLog : BaseEntity
{
    /// <summary>HubSpot contact hs_object_id, if known.</summary>
    public string? ContactHsId { get; set; }

    /// <summary>Recipient / sender phone number.</summary>
    public string ContactPhone { get; set; } = string.Empty;

    /// <summary>Contact display name (first + last) at time of send.</summary>
    public string? ContactName { get; set; }

    /// <summary>Sent = outbound from the system, Received = inbound.</summary>
    public SmsDirection Direction { get; set; } = SmsDirection.Sent;

    /// <summary>Full message body that was sent.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>IPPanel messages_outbox_id returned after a successful send.</summary>
    public string? IpPanelMessageId { get; set; }

    /// <summary>Sender number (خط ارسال).</summary>
    public string? SenderNumber { get; set; }

    /// <summary>Pattern code used, or null for webservice SMS.</summary>
    public string? PatternCode { get; set; }

    /// <summary>IPPanel delivery status (e.g. "sent", "failed").</summary>
    public string? Status { get; set; }

    /// <summary>When the message was (or should be) sent.</summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public enum SmsDirection
{
    Sent = 0,
    Received = 1
}

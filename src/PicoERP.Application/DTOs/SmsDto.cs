using PicoERP.Domain.Entities;

namespace PicoERP.Application.DTOs;

/// <summary>Data-transfer object for an SMS log entry.</summary>
public class SmsLogDto
{
    public int Id { get; set; }
    public string? ContactHsId { get; set; }
    public string ContactPhone { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public SmsDirection Direction { get; set; } = SmsDirection.Sent;
    public string Message { get; set; } = string.Empty;
    public string? IpPanelMessageId { get; set; }
    public string? SenderNumber { get; set; }
    public string? PatternCode { get; set; }
    public string? Status { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; }
}

/// <summary>IPPanel pattern DTO.</summary>
public class IpPanelPatternDto
{
    public string Id { get; set; } = string.Empty;
    public string PatternCode { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string PatternMessage { get; set; } = string.Empty;
    public string? PatternDescription { get; set; }
    public string PatternStatus { get; set; } = string.Empty;
    public string? PatternStatusFa { get; set; }
    public bool PatternIsShare { get; set; }
    public string PatternType { get; set; } = "normal";
    public List<IpPanelPatternVariable> Variable { get; set; } = new();
    public string Delimiter { get; set; } = "%";
    public DateTime? UpdatedAt { get; set; }
}

public class IpPanelPatternVariable
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public int Len { get; set; }
}

/// <summary>Form model for creating / updating a pattern.</summary>
public class CreatePatternDto
{
    public string? Title { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsShare { get; set; } = false;
    public string Message { get; set; } = string.Empty;
    public string? Website { get; set; }
    public List<IpPanelPatternVariable> Variable { get; set; } = new();
}

/// <summary>IPPanel outbox report item.</summary>
public class IpPanelOutboxReportDto
{
    public string? MessagesOutboxId { get; set; }
    public string? Number { get; set; }
    public string? Message { get; set; }
    public string? Status { get; set; }
    public string? Type { get; set; }
    public string? TimeSend { get; set; }
    public string? RcptsCount { get; set; }
    public string? ExitCount { get; set; }
    public decimal? Cost { get; set; }
    public int? StateId { get; set; }
}

using PicoERP.Domain.Common;

namespace PicoERP.Domain.Entities;

public class Income : BaseEntity
{
    public int CategoryId { get; set; }
    public IncomeCategory Category { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan? Time { get; set; }
    public string? Description { get; set; }
    public int? FinancialAccountId { get; set; }
    public FinancialAccount? FinancialAccount { get; set; }
    public string? AttachmentPath { get; set; }
    public string? RegisteredBy { get; set; }
    public string? InvoiceNumber { get; set; }
}

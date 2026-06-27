using PicoERP.Domain.Common;
using PicoERP.Domain.Enums;

namespace PicoERP.Domain.Entities;

public class Expense : BaseEntity
{
    public int CategoryId { get; set; }
    public ExpenseCategory Category { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string? Description { get; set; }
    public string? InvoicePath { get; set; }
    public string? AttachmentPath { get; set; }
    public int? FinancialAccountId { get; set; }
    public FinancialAccount? FinancialAccount { get; set; }
    public string? Tags { get; set; }
    public bool IsApproved { get; set; }
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public ExpenseGroup Group { get; set; }
    public string? RegisteredBy { get; set; }
    public string? InvoiceNumber { get; set; }
}

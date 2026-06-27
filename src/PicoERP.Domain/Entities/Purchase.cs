using PicoERP.Domain.Common;
using PicoERP.Domain.Enums;

namespace PicoERP.Domain.Entities;

public class Purchase : BaseEntity
{
    public string VendorName { get; set; } = string.Empty;
    public string? InvoiceNumber { get; set; }
    public string? InvoiceImagePath { get; set; }
    public ExpenseGroup PurchaseType { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime Date { get; set; }
    public int? FinancialAccountId { get; set; }
    public FinancialAccount? FinancialAccount { get; set; }
    public string? AttachmentPath { get; set; }
    public string? Description { get; set; }
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string? RegisteredBy { get; set; }
}

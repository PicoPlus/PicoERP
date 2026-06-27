using PicoERP.Domain.Enums;

namespace PicoERP.Application.DTOs;

public class IncomeDto
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan? Time { get; set; }
    public string? Description { get; set; }
    public int? FinancialAccountId { get; set; }
    public string? AccountName { get; set; }
    public string? RegisteredBy { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateIncomeDto
{
    public int CategoryId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public string? Description { get; set; }
    public int? FinancialAccountId { get; set; }
    public string? InvoiceNumber { get; set; }
}

public class UpdateIncomeDto : CreateIncomeDto
{
    public int Id { get; set; }
}

public class IncomeCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; }
    public decimal TotalAmount { get; set; }
}

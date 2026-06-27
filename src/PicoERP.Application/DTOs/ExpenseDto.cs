using PicoERP.Domain.Enums;

namespace PicoERP.Application.DTOs;

public class ExpenseDto
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public ExpenseGroup Group { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string? Description { get; set; }
    public string? InvoicePath { get; set; }
    public int? FinancialAccountId { get; set; }
    public string? AccountName { get; set; }
    public string? Tags { get; set; }
    public bool IsApproved { get; set; }
    public int? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? RegisteredBy { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateExpenseDto
{
    public int CategoryId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public string? Description { get; set; }
    public int? FinancialAccountId { get; set; }
    public string? Tags { get; set; }
    public int? EmployeeId { get; set; }
    public ExpenseGroup Group { get; set; }
    public string? InvoiceNumber { get; set; }
}

public class UpdateExpenseDto : CreateExpenseDto
{
    public int Id { get; set; }
    public bool IsApproved { get; set; }
}

public class ExpenseCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ExpenseGroup Group { get; set; }
    public int? ParentCategoryId { get; set; }
    public string? ParentName { get; set; }
    public bool IsActive { get; set; }
    public decimal TotalAmount { get; set; }
}

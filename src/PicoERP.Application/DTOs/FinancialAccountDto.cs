using PicoERP.Domain.Enums;

namespace PicoERP.Application.DTOs;

public class FinancialAccountDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? CardNumber { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
}

public class AccountTransferDto
{
    public int Id { get; set; }
    public int FromAccountId { get; set; }
    public string FromAccountName { get; set; } = string.Empty;
    public int ToAccountId { get; set; }
    public string ToAccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string? Description { get; set; }
    public string? RegisteredBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateAccountTransferDto
{
    public int FromAccountId { get; set; }
    public int ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public string? Description { get; set; }
}

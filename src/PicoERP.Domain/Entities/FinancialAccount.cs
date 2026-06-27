using PicoERP.Domain.Common;
using PicoERP.Domain.Enums;

namespace PicoERP.Domain.Entities;

public class FinancialAccount : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? CardNumber { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Color { get; set; }
    public string? Icon { get; set; }

    // Navigation
    public ICollection<Income> Incomes { get; set; } = new List<Income>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<AccountTransfer> TransfersFrom { get; set; } = new List<AccountTransfer>();
    public ICollection<AccountTransfer> TransfersTo { get; set; } = new List<AccountTransfer>();
    public ICollection<SalaryPayment> SalaryPayments { get; set; } = new List<SalaryPayment>();
}

using PicoERP.Domain.Common;

namespace PicoERP.Domain.Entities;

public class SalaryPayment : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal Overtime { get; set; }
    public decimal Bonus { get; set; }
    public decimal Advance { get; set; }    // مساعده
    public decimal Fine { get; set; }       // جریمه
    public decimal Insurance { get; set; }
    public decimal Tax { get; set; }
    public decimal NetSalary { get; set; }
    public int? FinancialAccountId { get; set; }
    public FinancialAccount? FinancialAccount { get; set; }
    public string? Notes { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
}

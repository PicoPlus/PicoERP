namespace PicoERP.Application.DTOs;

public class SalaryPaymentDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal Overtime { get; set; }
    public decimal Bonus { get; set; }
    public decimal Advance { get; set; }
    public decimal Fine { get; set; }
    public decimal Insurance { get; set; }
    public decimal Tax { get; set; }
    public decimal NetSalary { get; set; }
    public int? FinancialAccountId { get; set; }
    public string? AccountName { get; set; }
    public string? Notes { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateSalaryPaymentDto
{
    public int EmployeeId { get; set; }
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public decimal BaseSalary { get; set; }
    /// <summary>Set by the UI for percentage-based employees; service uses it to auto-compute BaseSalary.</summary>
    public decimal SalaryPercentage { get; set; }
    public decimal Overtime { get; set; }
    public decimal Bonus { get; set; }
    public decimal Advance { get; set; }
    public decimal Fine { get; set; }
    public decimal Insurance { get; set; }
    public decimal Tax { get; set; }
    public int? FinancialAccountId { get; set; }
    public string? Notes { get; set; }
}

public class AttendanceDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public TimeSpan? CheckIn { get; set; }
    public TimeSpan? CheckOut { get; set; }
    public Domain.Enums.AttendanceType Type { get; set; }
    public decimal OvertimeHours { get; set; }
    public string? Notes { get; set; }
}

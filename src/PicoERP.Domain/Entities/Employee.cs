using PicoERP.Domain.Common;
using PicoERP.Domain.Enums;

namespace PicoERP.Domain.Entities;

public class Employee : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Address { get; set; }
    public DateTime HireDate { get; set; }
    public string Position { get; set; } = string.Empty;

    /// <summary>Determines whether the employee earns a fixed amount or a % of revenue.</summary>
    public SalaryType SalaryType { get; set; } = SalaryType.Fixed;

    /// <summary>Used when SalaryType == Fixed. Monthly gross amount in local currency.</summary>
    public decimal BaseSalary { get; set; }

    /// <summary>Used when SalaryType == PercentageOfRevenue. Value is 0–100 (e.g. 10 means 10 %).</summary>
    public decimal SalaryPercentage { get; set; }

    public bool HasInsurance { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public string? PhotoPath { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    public ICollection<SalaryPayment> SalaryPayments { get; set; } = new List<SalaryPayment>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    public string FullName => $"{FirstName} {LastName}";
}

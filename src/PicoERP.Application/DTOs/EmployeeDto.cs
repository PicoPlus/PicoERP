using PicoERP.Domain.Enums;

namespace PicoERP.Application.DTOs;

public class EmployeeDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string NationalId { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Address { get; set; }
    public DateTime HireDate { get; set; }
    public string Position { get; set; } = string.Empty;
    public SalaryType SalaryType { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal SalaryPercentage { get; set; }
    public bool HasInsurance { get; set; }
    public EmployeeStatus Status { get; set; }
    public string? PhotoPath { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateEmployeeDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Address { get; set; }
    public DateTime HireDate { get; set; } = DateTime.Today;
    public string Position { get; set; } = string.Empty;
    public SalaryType SalaryType { get; set; } = SalaryType.Fixed;
    public decimal BaseSalary { get; set; }
    public decimal SalaryPercentage { get; set; }
    public bool HasInsurance { get; set; }
    public string? Notes { get; set; }
}

public class UpdateEmployeeDto : CreateEmployeeDto
{
    public int Id { get; set; }
    public EmployeeStatus Status { get; set; }
}

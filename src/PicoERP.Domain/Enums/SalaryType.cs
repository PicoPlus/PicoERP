namespace PicoERP.Domain.Enums;

public enum SalaryType
{
    /// <summary>Employee receives a fixed monthly amount stored in BaseSalary.</summary>
    Fixed = 0,

    /// <summary>Employee receives a percentage of the period's total revenue; SalaryPercentage holds the rate (e.g. 10 = 10 %).</summary>
    PercentageOfRevenue = 1
}

using PicoERP.Application.Common;
using PicoERP.Application.DTOs;

namespace PicoERP.Application.Interfaces;

public interface ISalaryService
{
    Task<PagedResult<SalaryPaymentDto>> GetPagedAsync(PaginationParams paging, int? employeeId = null);
    Task<SalaryPaymentDto?> GetByIdAsync(int id);
    Task<Result<SalaryPaymentDto>> CreateAsync(CreateSalaryPaymentDto dto, string registeredBy);
    Task<Result> MarkAsPaidAsync(int id, int financialAccountId);
    Task<Result> DeleteAsync(int id);
    Task<decimal> GetUnpaidTotalAsync();
    Task<byte[]> GenerateSlipPdfAsync(int id);

    /// <summary>
    /// Returns the total income recorded between <paramref name="from"/> and <paramref name="to"/>
    /// so the UI can preview the computed salary for a percentage-based employee.
    /// </summary>
    Task<decimal> GetPeriodRevenueAsync(DateTime from, DateTime to);

    /// <summary>
    /// Returns the outstanding (unpaid) salary balance per employee:
    /// sum of all NetSalary on unpaid salary records for that employee.
    /// Key = EmployeeId, Value = total unpaid net salary.
    /// </summary>
    Task<Dictionary<int, decimal>> GetUnpaidPerEmployeeAsync();

    /// <summary>
    /// Auto-generates a salary record for every active Fixed-salary employee that does NOT
    /// yet have a salary entry for the current Persian month. Returns the number of records created.
    /// </summary>
    Task<int> AutoGenerateCurrentMonthAsync(string registeredBy);
}

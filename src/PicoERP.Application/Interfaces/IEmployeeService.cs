using PicoERP.Application.Common;
using PicoERP.Application.DTOs;

namespace PicoERP.Application.Interfaces;

public interface IEmployeeService
{
    Task<PagedResult<EmployeeDto>> GetPagedAsync(PaginationParams paging);
    Task<EmployeeDto?> GetByIdAsync(int id);
    Task<Result<EmployeeDto>> CreateAsync(CreateEmployeeDto dto);
    Task<Result<EmployeeDto>> UpdateAsync(UpdateEmployeeDto dto);
    Task<Result> DeleteAsync(int id);
    Task<List<EmployeeDto>> GetAllActiveAsync();
    Task<List<AttendanceDto>> GetAttendanceAsync(int employeeId, DateTime from, DateTime to);
    Task<Result<AttendanceDto>> RecordAttendanceAsync(AttendanceDto dto);
}

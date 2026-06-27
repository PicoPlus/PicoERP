using Microsoft.EntityFrameworkCore;
using PicoERP.Application.Common;
using PicoERP.Application.DTOs;
using PicoERP.Application.Interfaces;
using PicoERP.Domain.Entities;
using PicoERP.Domain.Enums;
using PicoERP.Infrastructure.Persistence;

namespace PicoERP.Infrastructure.Services;

public class EmployeeService : IEmployeeService
{
    private readonly AppDbContext _db;
    public EmployeeService(AppDbContext db) => _db = db;

    public async Task<PagedResult<EmployeeDto>> GetPagedAsync(PaginationParams paging)
    {
        var query = _db.Employees.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(paging.Search))
            query = query.Where(e => e.FirstName.Contains(paging.Search) || e.LastName.Contains(paging.Search) || e.NationalId.Contains(paging.Search));

        int total = await query.CountAsync();
        var items = await query
            .OrderBy(e => e.FirstName)
            .Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)
            .Select(e => MapDto(e)).ToListAsync();

        return new PagedResult<EmployeeDto> { Items = items, TotalCount = total, Page = paging.Page, PageSize = paging.PageSize };
    }

    public async Task<EmployeeDto?> GetByIdAsync(int id)
    {
        var e = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return e == null ? null : MapDto(e);
    }

    public async Task<Result<EmployeeDto>> CreateAsync(CreateEmployeeDto dto)
    {
        // IgnoreQueryFilters so soft-deleted records are also checked — the DB unique
        // index is unconditional and would reject the INSERT regardless.
        if (await _db.Employees.IgnoreQueryFilters().AnyAsync(e => e.NationalId == dto.NationalId))
            return Result<EmployeeDto>.Failure("کد ملی تکراری است");

        var entity = new Employee
        {
            FirstName = dto.FirstName, LastName = dto.LastName,
            NationalId = dto.NationalId, PhoneNumber = dto.PhoneNumber,
            Address = dto.Address, HireDate = dto.HireDate,
            Position = dto.Position, SalaryType = dto.SalaryType,
            BaseSalary = dto.BaseSalary, SalaryPercentage = dto.SalaryPercentage,
            HasInsurance = dto.HasInsurance, Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow
        };
        _db.Employees.Add(entity);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return Result<EmployeeDto>.Failure("کد ملی تکراری است");
        }
        return Result<EmployeeDto>.Success(MapDto(entity));
    }

    public async Task<Result<EmployeeDto>> UpdateAsync(UpdateEmployeeDto dto)
    {
        var entity = await _db.Employees.FindAsync(dto.Id);
        if (entity == null) return Result<EmployeeDto>.Failure("کارمند یافت نشد");

        if (await _db.Employees.AnyAsync(e => e.NationalId == dto.NationalId && e.Id != dto.Id))
            return Result<EmployeeDto>.Failure("کد ملی تکراری است");

        entity.FirstName = dto.FirstName; entity.LastName = dto.LastName;
        entity.NationalId = dto.NationalId; entity.PhoneNumber = dto.PhoneNumber;
        entity.Address = dto.Address; entity.HireDate = dto.HireDate;
        entity.Position = dto.Position; entity.SalaryType = dto.SalaryType;
        entity.BaseSalary = dto.BaseSalary; entity.SalaryPercentage = dto.SalaryPercentage;
        entity.HasInsurance = dto.HasInsurance; entity.Notes = dto.Notes;
        entity.Status = dto.Status; entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result<EmployeeDto>.Success(MapDto(entity));
    }

    public async Task<Result> DeleteAsync(int id)
    {
        var entity = await _db.Employees.FindAsync(id);
        if (entity == null) return Result.Failure("یافت نشد");
        entity.IsDeleted = true; entity.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<List<EmployeeDto>> GetAllActiveAsync()
    {
        return await _db.Employees.AsNoTracking()
            .Where(e => e.Status == EmployeeStatus.Active)
            .Select(e => MapDto(e)).ToListAsync();
    }

    public async Task<List<AttendanceDto>> GetAttendanceAsync(int employeeId, DateTime from, DateTime to)
    {
        return await _db.AttendanceRecords.AsNoTracking()
            .Include(a => a.Employee)
            .Where(a => a.EmployeeId == employeeId && a.Date >= from && a.Date <= to)
            .OrderByDescending(a => a.Date)
            .Select(a => new AttendanceDto
            {
                Id = a.Id, EmployeeId = a.EmployeeId,
                EmployeeName = $"{a.Employee.FirstName} {a.Employee.LastName}",
                Date = a.Date, CheckIn = a.CheckIn, CheckOut = a.CheckOut,
                Type = a.Type, OvertimeHours = a.OvertimeHours, Notes = a.Notes
            }).ToListAsync();
    }

    public async Task<Result<AttendanceDto>> RecordAttendanceAsync(AttendanceDto dto)
    {
        var entity = dto.Id > 0 ? await _db.AttendanceRecords.FindAsync(dto.Id) : null;
        if (entity == null)
        {
            entity = new AttendanceRecord
            {
                EmployeeId = dto.EmployeeId, Date = dto.Date,
                CheckIn = dto.CheckIn, CheckOut = dto.CheckOut,
                Type = dto.Type, OvertimeHours = dto.OvertimeHours, Notes = dto.Notes,
                CreatedAt = DateTime.UtcNow
            };
            _db.AttendanceRecords.Add(entity);
        }
        else
        {
            entity.CheckIn = dto.CheckIn; entity.CheckOut = dto.CheckOut;
            entity.Type = dto.Type; entity.OvertimeHours = dto.OvertimeHours;
            entity.Notes = dto.Notes; entity.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        dto.Id = entity.Id;
        return Result<AttendanceDto>.Success(dto);
    }

    private static EmployeeDto MapDto(Employee e) => new()
    {
        Id = e.Id, FirstName = e.FirstName, LastName = e.LastName,
        NationalId = e.NationalId, PhoneNumber = e.PhoneNumber,
        Address = e.Address, HireDate = e.HireDate, Position = e.Position,
        SalaryType = e.SalaryType, BaseSalary = e.BaseSalary,
        SalaryPercentage = e.SalaryPercentage, HasInsurance = e.HasInsurance,
        Status = e.Status, PhotoPath = e.PhotoPath, Notes = e.Notes,
        CreatedAt = e.CreatedAt
    };
}

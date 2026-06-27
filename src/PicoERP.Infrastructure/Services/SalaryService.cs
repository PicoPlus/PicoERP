using Microsoft.EntityFrameworkCore;
using PicoERP.Application.Common;
using PicoERP.Application.DTOs;
using PicoERP.Application.Interfaces;
using PicoERP.Domain.Entities;
using PicoERP.Infrastructure.Persistence;

namespace PicoERP.Infrastructure.Services;

public class SalaryService : ISalaryService
{
    private readonly AppDbContext _db;
    public SalaryService(AppDbContext db) => _db = db;

    public async Task<PagedResult<SalaryPaymentDto>> GetPagedAsync(PaginationParams paging, int? employeeId = null)
    {
        var query = _db.SalaryPayments.AsNoTracking()
            .Include(s => s.Employee).Include(s => s.FinancialAccount).AsQueryable();
        if (employeeId.HasValue) query = query.Where(s => s.EmployeeId == employeeId.Value);

        int total = await query.CountAsync();
        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)
            .Select(s => MapDto(s)).ToListAsync();

        return new PagedResult<SalaryPaymentDto> { Items = items, TotalCount = total, Page = paging.Page, PageSize = paging.PageSize };
    }

    public async Task<SalaryPaymentDto?> GetByIdAsync(int id)
    {
        var s = await _db.SalaryPayments.AsNoTracking()
            .Include(x => x.Employee).Include(x => x.FinancialAccount)
            .FirstOrDefaultAsync(x => x.Id == id);
        return s == null ? null : MapDto(s);
    }

    public async Task<Result<SalaryPaymentDto>> CreateAsync(CreateSalaryPaymentDto dto, string registeredBy)
    {
        // Auto-compute BaseSalary for percentage-based employees when the caller passes 0
        if (dto.BaseSalary == 0 && dto.SalaryPercentage > 0)
        {
            var revenue = await GetPeriodRevenueAsync(dto.PeriodFrom, dto.PeriodTo);
            dto.BaseSalary = Math.Round(revenue * dto.SalaryPercentage / 100m, 0);
        }

        var net = dto.BaseSalary + dto.Overtime + dto.Bonus - dto.Fine;

        var entity = new SalaryPayment
        {
            EmployeeId = dto.EmployeeId, PeriodFrom = dto.PeriodFrom, PeriodTo = dto.PeriodTo,
            BaseSalary = dto.BaseSalary, Overtime = dto.Overtime, Bonus = dto.Bonus,
            Advance = dto.Advance, Fine = dto.Fine, Insurance = dto.Insurance, Tax = dto.Tax,
            NetSalary = net, FinancialAccountId = dto.FinancialAccountId, Notes = dto.Notes,
            CreatedBy = registeredBy, CreatedAt = DateTime.UtcNow
        };
        _db.SalaryPayments.Add(entity);

        // No Expense is created here. Expense is only recorded in MarkAsPaidAsync
        // so the salary does NOT appear in monthly expense totals until actually paid.

        await _db.SaveChangesAsync();
        return Result<SalaryPaymentDto>.Success((await GetByIdAsync(entity.Id))!);
    }

    private void AddExpense(decimal amount, int categoryId, DateTime date, string description,
                            int? accountId, Domain.Enums.ExpenseGroup group, int employeeId, string registeredBy)
    {
        _db.Expenses.Add(new Expense
        {
            CategoryId = categoryId, Amount = amount, Date = date,
            Description = description, FinancialAccountId = accountId,
            Group = group, EmployeeId = employeeId,
            RegisteredBy = registeredBy, CreatedAt = DateTime.UtcNow
        });
    }

    public async Task<Result> MarkAsPaidAsync(int id, int financialAccountId)
    {
        var entity = await _db.SalaryPayments
            .Include(s => s.Employee)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) return Result.Failure("یافت نشد");

        bool alreadyPaid = entity.IsPaid;
        entity.IsPaid = true;
        entity.PaidAt = DateTime.UtcNow;
        entity.FinancialAccountId = financialAccountId;
        entity.UpdatedAt = DateTime.UtcNow;

        // Only execute on the first payment — not if accidentally called twice
        if (!alreadyPaid)
        {
            // 1. Deduct from financial account
            var acct = await _db.FinancialAccounts.FindAsync(financialAccountId);
            if (acct != null) acct.CurrentBalance -= entity.NetSalary;

            // 2. Now record the Expense (salary only hits expense report when paid)
            string empName = entity.Employee != null
                ? $"{entity.Employee.FirstName} {entity.Employee.LastName}"
                : $"کارمند {entity.EmployeeId}";

            int salaryCatId = await _db.ExpenseCategories
                .Where(c => c.Name == "حقوق")
                .Select(c => c.Id)
                .FirstOrDefaultAsync();
            int employeeCatId = await _db.ExpenseCategories
                .Where(c => c.Group == Domain.Enums.ExpenseGroup.Employee)
                .OrderBy(c => c.Id)
                .Select(c => c.Id)
                .FirstOrDefaultAsync();
            if (salaryCatId == 0) salaryCatId = employeeCatId;

            if (entity.NetSalary > 0)
                AddExpense(entity.NetSalary, salaryCatId, entity.PeriodTo,
                           $"حقوق {empName}", financialAccountId,
                           Domain.Enums.ExpenseGroup.Employee, entity.EmployeeId,
                           entity.CreatedBy ?? "system");

            if (entity.Fine > 0)
                AddExpense(entity.Fine, employeeCatId, entity.PeriodTo,
                           $"جریمه {empName}", financialAccountId,
                           Domain.Enums.ExpenseGroup.Employee, entity.EmployeeId,
                           entity.CreatedBy ?? "system");
        }

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int id)
    {
        var entity = await _db.SalaryPayments.FindAsync(id);
        if (entity == null) return Result.Failure("یافت نشد");
        entity.IsDeleted = true; entity.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<decimal> GetUnpaidTotalAsync()
        => await _db.SalaryPayments.Where(s => !s.IsPaid && !s.IsDeleted).SumAsync(s => (decimal?)s.NetSalary) ?? 0;

    public async Task<decimal> GetPeriodRevenueAsync(DateTime from, DateTime to)
    {
        // Make `to` inclusive of the entire last day regardless of any stored time component
        var toEndOfDay = to.Date.AddDays(1);
        return await _db.Incomes
            .Where(i => i.Date >= from.Date && i.Date < toEndOfDay)
            .SumAsync(i => (decimal?)i.Amount) ?? 0;
    }

    public async Task<Dictionary<int, decimal>> GetUnpaidPerEmployeeAsync()
    {
        // Remaining balance = unpaid NetSalary minus any Advance already drawn by the employee
        return await _db.SalaryPayments
            .Where(s => !s.IsPaid && !s.IsDeleted)
            .GroupBy(s => s.EmployeeId)
            .Select(g => new
            {
                EmployeeId = g.Key,
                // NetSalary already has Fine deducted; subtract Advance (مساعده) drawn so far
                Total = g.Sum(s => s.NetSalary) - g.Sum(s => s.Advance)
            })
            .Where(x => x.Total > 0)
            .ToDictionaryAsync(x => x.EmployeeId, x => x.Total);
    }

    public async Task<int> AutoGenerateCurrentMonthAsync(string registeredBy)
    {
        var now = DateTime.Now;
        int pYear  = Domain.Common.PersianCalendar.GetPersianYear(now);
        int pMonth = Domain.Common.PersianCalendar.GetPersianMonth(now);
        var monthStart = Domain.Common.PersianCalendar.GetPersianMonthStart(pYear, pMonth);
        var monthEnd   = Domain.Common.PersianCalendar.GetPersianMonthEnd(pYear, pMonth);

        var activeEmployees = await _db.Employees
            .Where(e => e.Status == Domain.Enums.EmployeeStatus.Active
                     && e.SalaryType == Domain.Enums.SalaryType.Fixed
                     && e.BaseSalary > 0)
            .ToListAsync();

        var existingEmployeeIds = await _db.SalaryPayments
            .Where(s => !s.IsDeleted && s.PeriodFrom >= monthStart && s.PeriodFrom <= monthEnd)
            .Select(s => s.EmployeeId)
            .Distinct()
            .ToListAsync();

        int created = 0;
        foreach (var emp in activeEmployees.Where(e => !existingEmployeeIds.Contains(e.Id)))
        {
            // Only the SalaryPayment record — NO Expense.
            // Expense is created only when actually paid (MarkAsPaidAsync).
            _db.SalaryPayments.Add(new SalaryPayment
            {
                EmployeeId = emp.Id,
                PeriodFrom = monthStart,
                PeriodTo   = monthEnd,
                BaseSalary = emp.BaseSalary,
                NetSalary  = emp.BaseSalary,
                IsPaid     = false,
                CreatedBy  = registeredBy,
                CreatedAt  = DateTime.UtcNow
            });
            created++;
        }

        if (created > 0)
            await _db.SaveChangesAsync();

        return created;
    }

    public async Task<byte[]> GenerateSlipPdfAsync(int id)
    {
        var slip = await GetByIdAsync(id);
        if (slip == null) return Array.Empty<byte>();
        return PdfGenerator.GenerateSalarySlip(slip);
    }

    private static SalaryPaymentDto MapDto(SalaryPayment s) => new()
    {
        Id = s.Id, EmployeeId = s.EmployeeId,
        EmployeeName = s.Employee != null ? $"{s.Employee.FirstName} {s.Employee.LastName}" : "",
        Position = s.Employee?.Position ?? "",
        PeriodFrom = s.PeriodFrom, PeriodTo = s.PeriodTo,
        BaseSalary = s.BaseSalary, Overtime = s.Overtime, Bonus = s.Bonus,
        Advance = s.Advance, Fine = s.Fine, Insurance = s.Insurance, Tax = s.Tax,
        NetSalary = s.NetSalary, FinancialAccountId = s.FinancialAccountId,
        AccountName = s.FinancialAccount?.Name, Notes = s.Notes,
        IsPaid = s.IsPaid, PaidAt = s.PaidAt, CreatedAt = s.CreatedAt
    };
}

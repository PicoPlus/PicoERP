using Microsoft.EntityFrameworkCore;
using PicoERP.Application.Common;
using PicoERP.Application.DTOs;
using PicoERP.Application.Interfaces;
using PicoERP.Domain.Entities;
using PicoERP.Domain.Enums;
using PicoERP.Infrastructure.Persistence;

namespace PicoERP.Infrastructure.Services;

public class ExpenseService : IExpenseService
{
    private readonly AppDbContext _db;
    public ExpenseService(AppDbContext db) => _db = db;

    public async Task<PagedResult<ExpenseDto>> GetPagedAsync(PaginationParams paging, DateTime? from = null, DateTime? to = null, int? categoryId = null, ExpenseGroup? group = null)
    {
        var query = _db.Expenses.AsNoTracking()
            .Include(e => e.Category)
            .Include(e => e.FinancialAccount)
            .Include(e => e.Employee)
            .AsQueryable();

        if (from.HasValue) query = query.Where(e => e.Date >= from.Value);
        if (to.HasValue) query = query.Where(e => e.Date <= to.Value);
        if (categoryId.HasValue) query = query.Where(e => e.CategoryId == categoryId.Value);
        if (group.HasValue) query = query.Where(e => e.Group == group.Value);
        if (!string.IsNullOrWhiteSpace(paging.Search))
            query = query.Where(e => e.Description!.Contains(paging.Search) || e.InvoiceNumber!.Contains(paging.Search));

        int total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt)
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .Select(e => MapDto(e))
            .ToListAsync();

        return new PagedResult<ExpenseDto> { Items = items, TotalCount = total, Page = paging.Page, PageSize = paging.PageSize };
    }

    public async Task<ExpenseDto?> GetByIdAsync(int id)
    {
        var e = await _db.Expenses.AsNoTracking()
            .Include(x => x.Category).Include(x => x.FinancialAccount).Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.Id == id);
        return e == null ? null : MapDto(e);
    }

    public async Task<Result<ExpenseDto>> CreateAsync(CreateExpenseDto dto, string registeredBy)
    {
        var entity = new Expense
        {
            CategoryId = dto.CategoryId,
            Amount = dto.Amount,
            Date = dto.Date,
            Description = dto.Description,
            FinancialAccountId = dto.FinancialAccountId,
            Tags = dto.Tags,
            EmployeeId = dto.EmployeeId,
            Group = dto.Group,
            InvoiceNumber = dto.InvoiceNumber,
            RegisteredBy = registeredBy,
            CreatedAt = DateTime.UtcNow
        };
        _db.Expenses.Add(entity);
        if (dto.FinancialAccountId.HasValue)
        {
            var acct = await _db.FinancialAccounts.FindAsync(dto.FinancialAccountId.Value);
            if (acct != null) acct.CurrentBalance -= dto.Amount;
        }
        await _db.SaveChangesAsync();
        return Result<ExpenseDto>.Success((await GetByIdAsync(entity.Id))!);
    }

    public async Task<Result<ExpenseDto>> UpdateAsync(UpdateExpenseDto dto)
    {
        var entity = await _db.Expenses.FindAsync(dto.Id);
        if (entity == null) return Result<ExpenseDto>.Failure("هزینه یافت نشد");

        if (entity.FinancialAccountId.HasValue)
        {
            var oldAcct = await _db.FinancialAccounts.FindAsync(entity.FinancialAccountId.Value);
            if (oldAcct != null) oldAcct.CurrentBalance += entity.Amount;
        }

        entity.CategoryId = dto.CategoryId;
        entity.Amount = dto.Amount;
        entity.Date = dto.Date;
        entity.Description = dto.Description;
        entity.FinancialAccountId = dto.FinancialAccountId;
        entity.Tags = dto.Tags;
        entity.IsApproved = dto.IsApproved;
        entity.EmployeeId = dto.EmployeeId;
        entity.InvoiceNumber = dto.InvoiceNumber;
        entity.UpdatedAt = DateTime.UtcNow;

        if (dto.FinancialAccountId.HasValue)
        {
            var newAcct = await _db.FinancialAccounts.FindAsync(dto.FinancialAccountId.Value);
            if (newAcct != null) newAcct.CurrentBalance -= dto.Amount;
        }
        await _db.SaveChangesAsync();
        return Result<ExpenseDto>.Success((await GetByIdAsync(entity.Id))!);
    }

    public async Task<Result> DeleteAsync(int id)
    {
        var entity = await _db.Expenses.FindAsync(id);
        if (entity == null) return Result.Failure("یافت نشد");
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        if (entity.FinancialAccountId.HasValue)
        {
            var acct = await _db.FinancialAccounts.FindAsync(entity.FinancialAccountId.Value);
            if (acct != null) acct.CurrentBalance += entity.Amount;
        }
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<decimal> GetTotalAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = _db.Expenses.AsQueryable();
        if (from.HasValue) query = query.Where(e => e.Date >= from.Value);
        if (to.HasValue) query = query.Where(e => e.Date <= to.Value);
        return await query.SumAsync(e => e.Amount);
    }

    public async Task<List<ExpenseCategoryDto>> GetCategoriesAsync()
    {
        return await _db.ExpenseCategories.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .Include(c => c.ParentCategory)
            .Select(c => new ExpenseCategoryDto
            {
                Id = c.Id, Name = c.Name, Group = c.Group,
                ParentCategoryId = c.ParentCategoryId,
                ParentName = c.ParentCategory != null ? c.ParentCategory.Name : null,
                IsActive = c.IsActive,
                TotalAmount = c.Expenses.Where(e => !e.IsDeleted).Sum(e => e.Amount)
            }).ToListAsync();
    }

    public async Task<List<ExpenseDto>> GetRecentAsync(int count = 5)
    {
        return await _db.Expenses.AsNoTracking()
            .Include(e => e.Category).Include(e => e.FinancialAccount).Include(e => e.Employee)
            .OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt)
            .Take(count)
            .Select(e => MapDto(e))
            .ToListAsync();
    }

    private static ExpenseDto MapDto(Expense e) => new()
    {
        Id = e.Id, CategoryId = e.CategoryId,
        CategoryName = e.Category?.Name ?? "",
        Group = e.Group,
        Amount = e.Amount, Date = e.Date,
        Description = e.Description,
        FinancialAccountId = e.FinancialAccountId,
        AccountName = e.FinancialAccount?.Name,
        Tags = e.Tags, IsApproved = e.IsApproved,
        EmployeeId = e.EmployeeId,
        EmployeeName = e.Employee != null ? $"{e.Employee.FirstName} {e.Employee.LastName}" : null,
        RegisteredBy = e.RegisteredBy,
        InvoiceNumber = e.InvoiceNumber,
        CreatedAt = e.CreatedAt
    };
}

using Microsoft.EntityFrameworkCore;
using PicoERP.Application.Common;
using PicoERP.Application.DTOs;
using PicoERP.Application.Interfaces;
using PicoERP.Domain.Entities;
using PicoERP.Domain.Enums;
using PicoERP.Infrastructure.Persistence;

namespace PicoERP.Infrastructure.Services;

public class IncomeService : IIncomeService
{
    private readonly AppDbContext _db;
    public IncomeService(AppDbContext db) => _db = db;

    public async Task<PagedResult<IncomeDto>> GetPagedAsync(PaginationParams paging, DateTime? from = null, DateTime? to = null, int? categoryId = null)
    {
        var query = _db.Incomes.AsNoTracking()
            .Include(i => i.Category)
            .Include(i => i.FinancialAccount)
            .AsQueryable();

        if (from.HasValue) query = query.Where(i => i.Date >= from.Value);
        if (to.HasValue) query = query.Where(i => i.Date <= to.Value);
        if (categoryId.HasValue) query = query.Where(i => i.CategoryId == categoryId.Value);
        if (!string.IsNullOrWhiteSpace(paging.Search))
            query = query.Where(i => i.Description!.Contains(paging.Search) || i.InvoiceNumber!.Contains(paging.Search));

        int total = await query.CountAsync();
        var items = await query
            .OrderByDescending(i => i.Date).ThenByDescending(i => i.CreatedAt)
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .Select(i => MapDto(i))
            .ToListAsync();

        return new PagedResult<IncomeDto> { Items = items, TotalCount = total, Page = paging.Page, PageSize = paging.PageSize };
    }

    public async Task<IncomeDto?> GetByIdAsync(int id)
    {
        var i = await _db.Incomes.AsNoTracking()
            .Include(x => x.Category).Include(x => x.FinancialAccount)
            .FirstOrDefaultAsync(x => x.Id == id);
        return i == null ? null : MapDto(i);
    }

    public async Task<Result<IncomeDto>> CreateAsync(CreateIncomeDto dto, string registeredBy)
    {
        var entity = new Income
        {
            CategoryId = dto.CategoryId,
            Amount = dto.Amount,
            Date = dto.Date,
            Description = dto.Description,
            FinancialAccountId = dto.FinancialAccountId,
            InvoiceNumber = dto.InvoiceNumber,
            RegisteredBy = registeredBy,
            CreatedAt = DateTime.UtcNow
        };
        _db.Incomes.Add(entity);
        if (dto.FinancialAccountId.HasValue)
        {
            var acct = await _db.FinancialAccounts.FindAsync(dto.FinancialAccountId.Value);
            if (acct != null) acct.CurrentBalance += dto.Amount;
        }
        await _db.SaveChangesAsync();
        return Result<IncomeDto>.Success((await GetByIdAsync(entity.Id))!);
    }

    public async Task<Result<IncomeDto>> UpdateAsync(UpdateIncomeDto dto)
    {
        var entity = await _db.Incomes.FindAsync(dto.Id);
        if (entity == null) return Result<IncomeDto>.Failure("درآمد یافت نشد");

        // Revert old balance
        if (entity.FinancialAccountId.HasValue)
        {
            var oldAcct = await _db.FinancialAccounts.FindAsync(entity.FinancialAccountId.Value);
            if (oldAcct != null) oldAcct.CurrentBalance -= entity.Amount;
        }

        entity.CategoryId = dto.CategoryId;
        entity.Amount = dto.Amount;
        entity.Date = dto.Date;
        entity.Description = dto.Description;
        entity.FinancialAccountId = dto.FinancialAccountId;
        entity.InvoiceNumber = dto.InvoiceNumber;
        entity.UpdatedAt = DateTime.UtcNow;

        if (dto.FinancialAccountId.HasValue)
        {
            var newAcct = await _db.FinancialAccounts.FindAsync(dto.FinancialAccountId.Value);
            if (newAcct != null) newAcct.CurrentBalance += dto.Amount;
        }
        await _db.SaveChangesAsync();
        return Result<IncomeDto>.Success((await GetByIdAsync(entity.Id))!);
    }

    public async Task<Result> DeleteAsync(int id)
    {
        var entity = await _db.Incomes.FindAsync(id);
        if (entity == null) return Result.Failure("یافت نشد");
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        if (entity.FinancialAccountId.HasValue)
        {
            var acct = await _db.FinancialAccounts.FindAsync(entity.FinancialAccountId.Value);
            if (acct != null) acct.CurrentBalance -= entity.Amount;
        }
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<decimal> GetTotalAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = _db.Incomes.AsQueryable();
        if (from.HasValue) query = query.Where(i => i.Date >= from.Value);
        if (to.HasValue) query = query.Where(i => i.Date <= to.Value);
        return await query.SumAsync(i => i.Amount);
    }

    public async Task<List<IncomeCategoryDto>> GetCategoriesAsync()
    {
        return await _db.IncomeCategories.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .Select(c => new IncomeCategoryDto
            {
                Id = c.Id, Name = c.Name, Description = c.Description,
                Icon = c.Icon, IsActive = c.IsActive,
                TotalAmount = c.Incomes.Where(i => !i.IsDeleted).Sum(i => i.Amount)
            }).ToListAsync();
    }

    public async Task<List<IncomeDto>> GetRecentAsync(int count = 5)
    {
        return await _db.Incomes.AsNoTracking()
            .Include(i => i.Category).Include(i => i.FinancialAccount)
            .OrderByDescending(i => i.Date).ThenByDescending(i => i.CreatedAt)
            .Take(count)
            .Select(i => MapDto(i))
            .ToListAsync();
    }

    private static IncomeDto MapDto(Income i) => new()
    {
        Id = i.Id, CategoryId = i.CategoryId,
        CategoryName = i.Category?.Name ?? "",
        Amount = i.Amount, Date = i.Date, Time = i.Time,
        Description = i.Description,
        FinancialAccountId = i.FinancialAccountId,
        AccountName = i.FinancialAccount?.Name,
        RegisteredBy = i.RegisteredBy,
        InvoiceNumber = i.InvoiceNumber,
        CreatedAt = i.CreatedAt
    };
}

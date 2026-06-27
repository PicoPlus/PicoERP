using Microsoft.EntityFrameworkCore;
using PicoERP.Application.Common;
using PicoERP.Application.DTOs;
using PicoERP.Application.Interfaces;
using PicoERP.Domain.Entities;
using PicoERP.Infrastructure.Persistence;

namespace PicoERP.Infrastructure.Services;

public class FinancialAccountService : IFinancialAccountService
{
    private readonly AppDbContext _db;
    public FinancialAccountService(AppDbContext db) => _db = db;

    public async Task<List<FinancialAccountDto>> GetAllAsync()
    {
        return await _db.FinancialAccounts.AsNoTracking()
            .Where(a => a.IsActive)
            .Select(a => MapDto(a)).ToListAsync();
    }

    public async Task<FinancialAccountDto?> GetByIdAsync(int id)
    {
        var a = await _db.FinancialAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return a == null ? null : MapDto(a);
    }

    public async Task<Result<FinancialAccountDto>> CreateAsync(FinancialAccountDto dto)
    {
        var entity = new FinancialAccount
        {
            Name = dto.Name, Type = dto.Type,
            OpeningBalance = dto.OpeningBalance,
            CurrentBalance = dto.OpeningBalance,
            BankName = dto.BankName, AccountNumber = dto.AccountNumber,
            CardNumber = dto.CardNumber, Description = dto.Description,
            IsActive = true, Color = dto.Color, Icon = dto.Icon,
            CreatedAt = DateTime.UtcNow
        };
        _db.FinancialAccounts.Add(entity);
        await _db.SaveChangesAsync();
        return Result<FinancialAccountDto>.Success(MapDto(entity));
    }

    public async Task<Result<FinancialAccountDto>> UpdateAsync(FinancialAccountDto dto)
    {
        var entity = await _db.FinancialAccounts.FindAsync(dto.Id);
        if (entity == null) return Result<FinancialAccountDto>.Failure("حساب یافت نشد");
        entity.Name = dto.Name; entity.Type = dto.Type;
        entity.BankName = dto.BankName; entity.AccountNumber = dto.AccountNumber;
        entity.CardNumber = dto.CardNumber; entity.Description = dto.Description;
        entity.Color = dto.Color; entity.Icon = dto.Icon;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result<FinancialAccountDto>.Success(MapDto(entity));
    }

    public async Task<Result> DeleteAsync(int id)
    {
        var entity = await _db.FinancialAccounts.FindAsync(id);
        if (entity == null) return Result.Failure("یافت نشد");
        entity.IsDeleted = true; entity.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<AccountTransferDto>> TransferAsync(CreateAccountTransferDto dto, string registeredBy)
    {
        if (dto.FromAccountId == dto.ToAccountId)
            return Result<AccountTransferDto>.Failure("حساب مبدا و مقصد نمی‌توانند یکی باشند");
        var from = await _db.FinancialAccounts.FindAsync(dto.FromAccountId);
        var to = await _db.FinancialAccounts.FindAsync(dto.ToAccountId);
        if (from == null || to == null) return Result<AccountTransferDto>.Failure("حساب یافت نشد");
        if (from.CurrentBalance < dto.Amount) return Result<AccountTransferDto>.Failure("موجودی کافی نیست");

        from.CurrentBalance -= dto.Amount;
        to.CurrentBalance += dto.Amount;

        var transfer = new AccountTransfer
        {
            FromAccountId = dto.FromAccountId, ToAccountId = dto.ToAccountId,
            Amount = dto.Amount, Date = dto.Date,
            Description = dto.Description, RegisteredBy = registeredBy,
            CreatedAt = DateTime.UtcNow
        };
        _db.AccountTransfers.Add(transfer);
        await _db.SaveChangesAsync();

        return Result<AccountTransferDto>.Success(new AccountTransferDto
        {
            Id = transfer.Id, FromAccountId = from.Id, FromAccountName = from.Name,
            ToAccountId = to.Id, ToAccountName = to.Name,
            Amount = transfer.Amount, Date = transfer.Date,
            Description = transfer.Description, RegisteredBy = transfer.RegisteredBy,
            CreatedAt = transfer.CreatedAt
        });
    }

    public async Task<List<AccountTransferDto>> GetTransfersAsync(int accountId)
    {
        return await _db.AccountTransfers.AsNoTracking()
            .Include(t => t.FromAccount).Include(t => t.ToAccount)
            .Where(t => t.FromAccountId == accountId || t.ToAccountId == accountId)
            .OrderByDescending(t => t.Date)
            .Select(t => new AccountTransferDto
            {
                Id = t.Id, FromAccountId = t.FromAccountId, FromAccountName = t.FromAccount.Name,
                ToAccountId = t.ToAccountId, ToAccountName = t.ToAccount.Name,
                Amount = t.Amount, Date = t.Date, Description = t.Description,
                RegisteredBy = t.RegisteredBy, CreatedAt = t.CreatedAt
            }).ToListAsync();
    }

    public async Task UpdateBalanceAsync(int accountId, decimal amount, bool isIncome)
    {
        var acct = await _db.FinancialAccounts.FindAsync(accountId);
        if (acct != null)
        {
            acct.CurrentBalance += isIncome ? amount : -amount;
            await _db.SaveChangesAsync();
        }
    }

    private static FinancialAccountDto MapDto(FinancialAccount a) => new()
    {
        Id = a.Id, Name = a.Name, Type = a.Type,
        OpeningBalance = a.OpeningBalance, CurrentBalance = a.CurrentBalance,
        BankName = a.BankName, AccountNumber = a.AccountNumber, CardNumber = a.CardNumber,
        Description = a.Description, IsActive = a.IsActive, Color = a.Color, Icon = a.Icon
    };
}

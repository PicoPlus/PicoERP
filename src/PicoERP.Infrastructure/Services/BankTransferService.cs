using Microsoft.EntityFrameworkCore;
using PicoERP.Application.Common;
using PicoERP.Application.DTOs;
using PicoERP.Application.Interfaces;
using PicoERP.Domain.Entities;
using PicoERP.Infrastructure.Persistence;

namespace PicoERP.Infrastructure.Services;

public class BankTransferService : IBankTransferService
{
    private readonly AppDbContext _db;
    public BankTransferService(AppDbContext db) => _db = db;

    // ── Receipts ──────────────────────────────────────────────────────────────

    public async Task<List<BankTransferReceiptDto>> GetReceiptsAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = _db.BankTransferReceipts.AsNoTracking()
            .Include(r => r.FinancialAccount)
            .Include(r => r.Payments).ThenInclude(p => p.FinancialAccount)
            .AsQueryable();

        if (from.HasValue) query = query.Where(r => r.Date >= from.Value);
        if (to.HasValue)   query = query.Where(r => r.Date <= to.Value);

        return await query.OrderByDescending(r => r.Date)
            .Select(r => MapReceiptDto(r)).ToListAsync();
    }

    public async Task<BankTransferReceiptDto?> GetReceiptByIdAsync(int id)
    {
        var r = await _db.BankTransferReceipts.AsNoTracking()
            .Include(r => r.FinancialAccount)
            .Include(r => r.Payments).ThenInclude(p => p.FinancialAccount)
            .FirstOrDefaultAsync(r => r.Id == id);
        return r == null ? null : MapReceiptDto(r);
    }

    public async Task<Result<BankTransferReceiptDto>> CreateReceiptAsync(CreateBankTransferReceiptDto dto, string registeredBy)
    {
        var entity = new BankTransferReceipt
        {
            TransactionId       = dto.TransactionId,
            PayerName           = dto.PayerName,
            Amount              = dto.Amount,
            Date                = dto.Date,
            FinancialAccountId  = dto.FinancialAccountId,
            Description         = dto.Description,
            RegisteredBy        = registeredBy,
            CreatedAt           = DateTime.UtcNow
        };
        _db.BankTransferReceipts.Add(entity);

        // Increase the receiving account's balance
        var account = await _db.FinancialAccounts.FindAsync(dto.FinancialAccountId);
        if (account != null) account.CurrentBalance += dto.Amount;

        await _db.SaveChangesAsync();
        return Result<BankTransferReceiptDto>.Success((await GetReceiptByIdAsync(entity.Id))!);
    }

    public async Task<Result> DeleteReceiptAsync(int id)
    {
        var entity = await _db.BankTransferReceipts
            .Include(r => r.Payments)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) return Result.Failure("یافت نشد");

        // Reverse the receipt's balance effect
        var account = await _db.FinancialAccounts.FindAsync(entity.FinancialAccountId);
        if (account != null) account.CurrentBalance -= entity.Amount;

        // Reverse any payment balance effects
        foreach (var p in entity.Payments.Where(p => !p.IsDeleted && p.FinancialAccountId.HasValue))
        {
            var payAcct = await _db.FinancialAccounts.FindAsync(p.FinancialAccountId!.Value);
            if (payAcct != null) payAcct.CurrentBalance += p.Amount;
        }

        entity.IsDeleted = true; entity.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    // ── Payments ─────────────────────────────────────────────────────────────

    public async Task<Result<BankTransferPaymentDto>> AddPaymentAsync(CreateBankTransferPaymentDto dto, string registeredBy)
    {
        var receipt = await _db.BankTransferReceipts
            .Include(r => r.Payments)
            .FirstOrDefaultAsync(r => r.Id == dto.ReceiptId);

        if (receipt == null) return Result<BankTransferPaymentDto>.Failure("رسید یافت نشد");

        var alreadyPaid = receipt.Payments.Where(p => !p.IsDeleted).Sum(p => p.Amount);
        if (alreadyPaid + dto.Amount > receipt.Amount)
            return Result<BankTransferPaymentDto>.Failure(
                $"مجموع پرداخت‌ها ({alreadyPaid + dto.Amount:N0}) از مبلغ رسید ({receipt.Amount:N0}) بیشتر می‌شود");

        var payment = new BankTransferPayment
        {
            ReceiptId          = dto.ReceiptId,
            TransactionId      = dto.TransactionId,
            RecipientName      = dto.RecipientName,
            Amount             = dto.Amount,
            Date               = dto.Date,
            FinancialAccountId = dto.FinancialAccountId,
            Purpose            = dto.Purpose,
            RegisteredBy       = registeredBy,
            CreatedAt          = DateTime.UtcNow
        };
        _db.BankTransferPayments.Add(payment);

        // Decrease the source account's balance (if one was selected)
        if (dto.FinancialAccountId.HasValue)
        {
            var acct = await _db.FinancialAccounts.FindAsync(dto.FinancialAccountId.Value);
            if (acct != null) acct.CurrentBalance -= dto.Amount;
        }

        await _db.SaveChangesAsync();

        return Result<BankTransferPaymentDto>.Success(MapPaymentDto(payment));
    }

    public async Task<Result> DeletePaymentAsync(int id)
    {
        var entity = await _db.BankTransferPayments.FindAsync(id);
        if (entity == null) return Result.Failure("یافت نشد");

        // Reverse the payment's balance effect
        if (entity.FinancialAccountId.HasValue)
        {
            var acct = await _db.FinancialAccounts.FindAsync(entity.FinancialAccountId.Value);
            if (acct != null) acct.CurrentBalance += entity.Amount;
        }

        entity.IsDeleted = true; entity.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<decimal> GetDayReceiptTotalAsync(DateTime date)
    {
        var d = date.Date;
        return await _db.BankTransferReceipts
            .Where(r => r.Date >= d && r.Date < d.AddDays(1))
            .SumAsync(r => (decimal?)r.Amount) ?? 0;
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static BankTransferReceiptDto MapReceiptDto(BankTransferReceipt r) => new()
    {
        Id                 = r.Id,
        TransactionId      = r.TransactionId,
        PayerName          = r.PayerName,
        Amount             = r.Amount,
        Date               = r.Date,
        FinancialAccountId = r.FinancialAccountId,
        AccountName        = r.FinancialAccount?.Name ?? "",
        Description        = r.Description,
        RegisteredBy       = r.RegisteredBy,
        CreatedAt          = r.CreatedAt,
        Payments           = r.Payments.Where(p => !p.IsDeleted).Select(MapPaymentDto).ToList()
    };

    private static BankTransferPaymentDto MapPaymentDto(BankTransferPayment p) => new()
    {
        Id                 = p.Id,
        ReceiptId          = p.ReceiptId,
        TransactionId      = p.TransactionId,
        RecipientName      = p.RecipientName,
        Amount             = p.Amount,
        Date               = p.Date,
        FinancialAccountId = p.FinancialAccountId,
        AccountName        = p.FinancialAccount?.Name,
        Purpose            = p.Purpose,
        RegisteredBy       = p.RegisteredBy,
        CreatedAt          = p.CreatedAt
    };
}

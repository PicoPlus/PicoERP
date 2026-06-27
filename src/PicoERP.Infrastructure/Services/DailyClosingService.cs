using Microsoft.EntityFrameworkCore;
using PicoERP.Application.Common;
using PicoERP.Application.DTOs;
using PicoERP.Application.Interfaces;
using PicoERP.Domain.Entities;
using PicoERP.Infrastructure.Persistence;

namespace PicoERP.Infrastructure.Services;

public class DailyClosingService : IDailyClosingService
{
    private readonly AppDbContext _db;
    public DailyClosingService(AppDbContext db) => _db = db;

    public async Task<DailyClosingSummaryDto> GetTodaySummaryAsync(DateTime date)
    {
        var d   = date.Date;
        var end = d.AddDays(1);

        // Auto-sum incomes recorded for the day, grouped by account
        var incomesByAccount = await _db.Incomes
            .Where(i => i.Date >= d && i.Date < end)
            .GroupBy(i => new { i.FinancialAccountId, AccName = i.FinancialAccount != null ? i.FinancialAccount.Name : "بدون حساب", AccType = i.FinancialAccount != null ? (int)i.FinancialAccount.Type : 0 })
            .Select(g => new { g.Key.FinancialAccountId, g.Key.AccName, g.Key.AccType, Total = g.Sum(i => i.Amount) })
            .ToListAsync();

        var expensesByAccount = await _db.Expenses
            .Where(e => e.Date >= d && e.Date < end)
            .GroupBy(e => new { e.FinancialAccountId, AccName = e.FinancialAccount != null ? e.FinancialAccount.Name : "بدون حساب" })
            .Select(g => new { g.Key.FinancialAccountId, g.Key.AccName, Total = g.Sum(e => e.Amount) })
            .ToListAsync();

        var autoBankTransfer = await _db.BankTransferReceipts
            .Where(r => r.Date >= d && r.Date < end)
            .SumAsync(r => (decimal?)r.Amount) ?? 0;

        var totalIncome  = incomesByAccount.Sum(x => x.Total);
        var totalExpense = expensesByAccount.Sum(x => x.Total);

        // Merge account breakdowns
        var allAccountIds = incomesByAccount.Select(x => x.FinancialAccountId)
            .Union(expensesByAccount.Select(x => x.FinancialAccountId))
            .Distinct().ToList();

        var breakdown = allAccountIds.Select(aid => new AccountDaySummaryDto
        {
            AccountId   = aid ?? 0,
            AccountName = incomesByAccount.FirstOrDefault(x => x.FinancialAccountId == aid)?.AccName
                       ?? expensesByAccount.FirstOrDefault(x => x.FinancialAccountId == aid)?.AccName
                       ?? "بدون حساب",
            AccountType = aid == null ? "" : incomesByAccount.FirstOrDefault(x => x.FinancialAccountId == aid)?.AccType switch
            {
                1 => "نقدی",
                2 => "بانک",
                3 => "کارتخوان",
                4 => "کیف پول",
                _ => ""
            } ?? "",
            TotalIncome  = incomesByAccount.Where(x => x.FinancialAccountId == aid).Sum(x => x.Total),
            TotalExpense = expensesByAccount.Where(x => x.FinancialAccountId == aid).Sum(x => x.Total),
        }).OrderByDescending(x => x.TotalIncome).ToList();

        var isClosed = await _db.DailyClosings.AnyAsync(c => !c.IsDeleted && c.Date.Date == d && c.IsFinalized);

        return new DailyClosingSummaryDto
        {
            Date                  = d,
            TotalIncomeRecorded   = totalIncome,
            AutoBankTransferTotal = autoBankTransfer,
            TotalExpense          = totalExpense,
            AccountBreakdown      = breakdown,
            IsClosed              = isClosed
        };
    }

    public async Task<List<DailyClosingDto>> GetHistoryAsync(int take = 30)
    {
        return await _db.DailyClosings.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderByDescending(c => c.Date)
            .Take(take)
            .Select(c => MapDto(c))
            .ToListAsync();
    }

    public async Task<Result<DailyClosingDto>> CloseAsync(CreateDailyClosingDto dto, string registeredBy)
    {
        var d   = dto.Date.Date;
        var end = d.AddDays(1);

        // Auto-compute bank-transfer total if caller left it at zero
        var bankTransfer = dto.BankTransferIncome > 0
            ? dto.BankTransferIncome
            : await _db.BankTransferReceipts.Where(r => r.Date >= d && r.Date < end).SumAsync(r => (decimal?)r.Amount) ?? 0;

        var totalIncome  = dto.CashOnHand + bankTransfer + dto.PosIncome + dto.OnlineIncome;
        var totalExpense = await _db.Expenses.Where(e => e.Date >= d && e.Date < end).SumAsync(e => (decimal?)e.Amount) ?? 0;

        var existing = await _db.DailyClosings.FirstOrDefaultAsync(c => !c.IsDeleted && c.Date.Date == d);
        if (existing != null)
        {
            existing.CashOnHand         = dto.CashOnHand;
            existing.BankTransferIncome = bankTransfer;
            existing.PosIncome          = dto.PosIncome;
            existing.OnlineIncome       = dto.OnlineIncome;
            existing.CashIncome         = dto.CashOnHand;
            existing.BankIncome         = bankTransfer;
            existing.CardIncome         = dto.PosIncome;
            existing.TotalIncome        = totalIncome;
            existing.TotalExpense       = totalExpense;
            existing.Profit             = totalIncome - totalExpense;
            existing.Notes              = dto.Notes;
            existing.IsFinalized        = true;
            existing.UpdatedAt          = DateTime.UtcNow;
        }
        else
        {
            existing = new DailyClosing
            {
                Date                = d,
                CashOnHand          = dto.CashOnHand,
                BankTransferIncome  = bankTransfer,
                PosIncome           = dto.PosIncome,
                OnlineIncome        = dto.OnlineIncome,
                CashIncome          = dto.CashOnHand,
                BankIncome          = bankTransfer,
                CardIncome          = dto.PosIncome,
                TotalIncome         = totalIncome,
                TotalExpense        = totalExpense,
                Profit              = totalIncome - totalExpense,
                Notes               = dto.Notes,
                RegisteredBy        = registeredBy,
                IsFinalized         = true,
                CreatedAt           = DateTime.UtcNow
            };
            _db.DailyClosings.Add(existing);
        }

        await _db.SaveChangesAsync();
        return Result<DailyClosingDto>.Success(MapDto(existing));
    }

    private static DailyClosingDto MapDto(DailyClosing c) => new()
    {
        Id                  = c.Id,
        Date                = c.Date,
        CashOnHand          = c.CashOnHand,
        BankTransferIncome  = c.BankTransferIncome,
        PosIncome           = c.PosIncome,
        OnlineIncome        = c.OnlineIncome,
        TotalIncome         = c.TotalIncome,
        TotalExpense        = c.TotalExpense,
        Profit              = c.Profit,
        Notes               = c.Notes,
        RegisteredBy        = c.RegisteredBy,
        IsFinalized         = c.IsFinalized,
        CreatedAt           = c.CreatedAt
    };
}

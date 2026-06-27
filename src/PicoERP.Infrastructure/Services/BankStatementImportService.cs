using Microsoft.EntityFrameworkCore;
using PicoERP.Application.Common;
using PicoERP.Application.DTOs;
using PicoERP.Application.Interfaces;
using PicoERP.Domain.Entities;
using PicoERP.Infrastructure.Persistence;

namespace PicoERP.Infrastructure.Services;

/// <summary>
/// Orchestrates bank-statement import: parser selection, date filtering,
/// duplicate detection, and persistence.
/// </summary>
public sealed class BankStatementImportService : IBankStatementImportService
{
    private readonly AppDbContext _db;
    private readonly IReadOnlyDictionary<string, IBankStatementParser> _parsers;

    public BankStatementImportService(
        AppDbContext db,
        IEnumerable<IBankStatementParser> parsers)
    {
        _db = db;
        _parsers = parsers.ToDictionary(p => p.BankKey, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<(string Key, string DisplayName)> GetSupportedBanks()
        => _parsers.Values.Select(p => (p.BankKey, p.DisplayName)).ToList();

    // ──────────────────────────────────────────────────────────────────────────
    // Preview (no persistence)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<Result<BankStatementPreviewDto>> PreviewAsync(BankStatementImportRequestDto request)
    {
        try
        {
            var (parser, parseError) = GetParser(request.BankKey);
            if (parser is null) return Result<BankStatementPreviewDto>.Failure(parseError!);

            var allRows = await parser.ParseAsync(request.FileBytes);

            var (startDate, endDate, dateError) = ParseDateRange(request.StartDatePersian, request.EndDatePersian);
            if (dateError is not null) return Result<BankStatementPreviewDto>.Failure(dateError);

            int totalInFile = allRows.Count;
            var filtered = ApplyDateFilter(allRows, startDate, endDate);
            int ignoredRows = totalInFile - filtered.Count;

            await MarkDuplicates(filtered, request.FinancialAccountId);

            // Pre-fill smart default action: deposit → Income, withdrawal → Expense
            foreach (var row in filtered.Where(r => !r.IsDuplicate))
            {
                if (row.DepositAmount > 0 && row.WithdrawalAmount == 0)
                    row.Action = BankStatementRowAction.Income;
                else if (row.WithdrawalAmount > 0 && row.DepositAmount == 0)
                    row.Action = BankStatementRowAction.Expense;
                else
                    row.Action = BankStatementRowAction.Ignore;
            }

            var preview = new BankStatementPreviewDto
            {
                Rows           = filtered,
                TotalRowsInFile = totalInFile,
                ValidRows       = filtered.Count(r => !r.IsDuplicate),
                DuplicateRows   = filtered.Count(r => r.IsDuplicate),
                IgnoredRows     = ignoredRows,
                TotalDeposits   = filtered.Where(r => !r.IsDuplicate).Sum(r => r.DepositAmount),
                TotalWithdrawals= filtered.Where(r => !r.IsDuplicate).Sum(r => r.WithdrawalAmount),
            };

            return Result<BankStatementPreviewDto>.Success(preview);
        }
        catch (InvalidOperationException ex)
        {
            return Result<BankStatementPreviewDto>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<BankStatementPreviewDto>.Failure($"خطا در پردازش فایل: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Commit (persist the already-previewed + user-classified rows)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<Result<BankStatementImportResultDto>> CommitAsync(BankStatementCommitRequestDto request)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var allRows  = request.PreviewedRows;
            var toImport = allRows
                .Where(r => !r.IsDuplicate && (r.Splits.Count > 0 || r.Action != BankStatementRowAction.Ignore))
                .ToList();
            int duplicate     = allRows.Count(r => r.IsDuplicate);
            int ignored       = allRows.Count(r => !r.IsDuplicate && r.Splits.Count == 0 && r.Action == BankStatementRowAction.Ignore);
            int incomeCount   = 0;
            int expenseCount  = 0;
            int transferCount = 0;

            // Load the financial account once (needed for balance updates)
            var account = await _db.FinancialAccounts.FindAsync(request.FinancialAccountId);

            foreach (var row in toImport)
            {
                var txDate = ParseTransactionDate(row.TransactionDateRaw) ?? DateTime.UtcNow;
                var parts  = row.TransactionDateRaw.Trim().Split(' ');
                TimeSpan? time = parts.Length > 1 && TimeSpan.TryParse(parts[1], out var t) ? t : null;

                if (row.Splits.Count > 0)
                {
                    // ── Split mode: persist each portion separately ───────────
                    foreach (var split in row.Splits.Where(s => s.Amount > 0))
                    {
                        if (split.Action == BankStatementRowAction.Income)
                        {
                            var income = new Income
                            {
                                CategoryId         = split.CategoryId ?? GetDefaultIncomeCategory(),
                                Amount             = split.Amount,
                                Date               = txDate,
                                Time               = time,
                                Description        = split.Description ?? row.Description ?? row.TransactionType,
                                FinancialAccountId = request.FinancialAccountId,
                                InvoiceNumber      = row.DocumentNumber,
                                RegisteredBy       = request.ImportedBy,
                                CreatedAt          = DateTime.UtcNow,
                            };
                            _db.Incomes.Add(income);
                            if (account != null) account.CurrentBalance += income.Amount;
                            incomeCount++;
                        }
                        else if (split.Action == BankStatementRowAction.Expense)
                        {
                            var catId = split.CategoryId ?? GetDefaultExpenseCategory();
                            var cat   = await _db.ExpenseCategories.FindAsync(catId);
                            var expense = new Expense
                            {
                                CategoryId         = catId,
                                Amount             = split.Amount,
                                Date               = txDate,
                                Description        = split.Description ?? row.Description ?? row.TransactionType,
                                FinancialAccountId = request.FinancialAccountId,
                                InvoiceNumber      = row.DocumentNumber,
                                Group              = cat?.Group ?? Domain.Enums.ExpenseGroup.Business,
                                RegisteredBy       = request.ImportedBy,
                                CreatedAt          = DateTime.UtcNow,
                            };
                            _db.Expenses.Add(expense);
                            if (account != null) account.CurrentBalance -= expense.Amount;
                            expenseCount++;
                        }
                        else if (split.Action == BankStatementRowAction.Transfer && split.ToAccountId.HasValue)
                        {
                            var xfer = new AccountTransfer
                            {
                                FromAccountId = request.FinancialAccountId,
                                ToAccountId   = split.ToAccountId.Value,
                                Amount        = split.Amount,
                                Date          = txDate,
                                Description   = split.Description ?? row.Description ?? row.TransactionType,
                                RegisteredBy  = request.ImportedBy,
                                CreatedAt     = DateTime.UtcNow,
                            };
                            _db.AccountTransfers.Add(xfer);
                            if (account != null) account.CurrentBalance -= split.Amount;
                            var toAcct = await _db.FinancialAccounts.FindAsync(split.ToAccountId.Value);
                            if (toAcct != null) toAcct.CurrentBalance += split.Amount;
                            transferCount++;
                        }
                    }
                }
                else if (row.Action == BankStatementRowAction.Transfer && row.ToAccountId.HasValue)
                {
                    var amount = row.WithdrawalAmount > 0 ? row.WithdrawalAmount : row.DepositAmount;
                    var xfer = new AccountTransfer
                    {
                        FromAccountId = request.FinancialAccountId,
                        ToAccountId   = row.ToAccountId.Value,
                        Amount        = amount,
                        Date          = txDate,
                        Description   = row.Description ?? row.TransactionType,
                        RegisteredBy  = request.ImportedBy,
                        CreatedAt     = DateTime.UtcNow,
                    };
                    _db.AccountTransfers.Add(xfer);
                    if (account != null) account.CurrentBalance -= amount;
                    var toAcct = await _db.FinancialAccounts.FindAsync(row.ToAccountId.Value);
                    if (toAcct != null) toAcct.CurrentBalance += amount;
                    transferCount++;
                }
                else if (row.Action == BankStatementRowAction.Income)
                {
                    // Amounts from bank statement are in Rial — store as-is
                    var income = new Income
                    {
                        CategoryId         = row.CategoryId ?? GetDefaultIncomeCategory(),
                        Amount             = row.DepositAmount,
                        Date               = txDate,
                        Time               = time,
                        Description        = row.Description ?? row.TransactionType,
                        FinancialAccountId = request.FinancialAccountId,
                        InvoiceNumber      = row.DocumentNumber,
                        RegisteredBy       = request.ImportedBy,
                        CreatedAt          = DateTime.UtcNow,
                    };
                    _db.Incomes.Add(income);
                    if (account != null) account.CurrentBalance += income.Amount;
                    incomeCount++;
                }
                else if (row.Action == BankStatementRowAction.Expense)
                {
                    var catId = row.CategoryId ?? GetDefaultExpenseCategory();
                    var cat   = await _db.ExpenseCategories.FindAsync(catId);
                    var expense = new Expense
                    {
                        CategoryId         = catId,
                        Amount             = row.WithdrawalAmount,
                        Date               = txDate,
                        Description        = row.Description ?? row.TransactionType,
                        FinancialAccountId = request.FinancialAccountId,
                        InvoiceNumber      = row.DocumentNumber,
                        Group              = cat?.Group ?? Domain.Enums.ExpenseGroup.Business,
                        RegisteredBy       = request.ImportedBy,
                        CreatedAt          = DateTime.UtcNow,
                    };
                    _db.Expenses.Add(expense);
                    if (account != null) account.CurrentBalance -= expense.Amount;
                    expenseCount++;
                }

                // Always record the raw bank transaction for audit
                var entity = MapToEntity(row, request.BankKey,
                                         request.FinancialAccountId, request.ImportedBy);
                _db.BankStatementTransactions.Add(entity);
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            var result = new BankStatementImportResultDto
            {
                IsSuccess        = true,
                TotalRowsInFile  = allRows.Count,
                ValidRows        = toImport.Count,
                ImportedRows     = toImport.Count,
                DuplicateRows    = duplicate,
                IgnoredRows      = ignored,
                IncomeCount      = incomeCount,
                ExpenseCount     = expenseCount,
                TransferCount    = transferCount,
                TotalDeposits    = toImport.Where(r => r.Splits.Count == 0 && r.Action == BankStatementRowAction.Income).Sum(r => r.DepositAmount),
                TotalWithdrawals = toImport.Where(r => r.Splits.Count == 0 && r.Action == BankStatementRowAction.Expense).Sum(r => r.WithdrawalAmount),
            };

            return Result<BankStatementImportResultDto>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync();
            return Result<BankStatementImportResultDto>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return Result<BankStatementImportResultDto>.Failure($"خطا در ذخیره‌سازی: {ex.Message}");
        }
    }

    // Returns the first available income/expense category id as fallback
    private int GetDefaultIncomeCategory()
        => _db.IncomeCategories.Where(c => !c.IsDeleted && c.IsActive)
              .OrderBy(c => c.Id).Select(c => c.Id).FirstOrDefault();

    private int GetDefaultExpenseCategory()
        => _db.ExpenseCategories.Where(c => !c.IsDeleted && c.IsActive)
              .OrderBy(c => c.Id).Select(c => c.Id).FirstOrDefault();

    // ──────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────────

    private (IBankStatementParser? parser, string? error) GetParser(string bankKey)
    {
        if (_parsers.TryGetValue(bankKey, out var parser)) return (parser, null);
        return (null, $"پارسر بانک '{bankKey}' پیدا نشد.");
    }

    /// <summary>
    /// Parses Persian date strings like "1405/04/01" and converts them to
    /// Gregorian DateTime using the domain's PersianCalendar helper.
    /// </summary>
    private static (DateTime? start, DateTime? end, string? error)
        ParseDateRange(string? startPersian, string? endPersian)
    {
        DateTime? start = null, end = null;

        if (!string.IsNullOrWhiteSpace(startPersian))
        {
            var parsed = ParsePersianDate(startPersian);
            if (parsed is null)
                return (null, null, $"تاریخ شروع نامعتبر است: {startPersian}");
            start = parsed;
        }

        if (!string.IsNullOrWhiteSpace(endPersian))
        {
            var parsed = ParsePersianDate(endPersian);
            if (parsed is null)
                return (null, null, $"تاریخ پایان نامعتبر است: {endPersian}");
            end = parsed.Value.AddDays(1).AddTicks(-1); // include the full day
        }

        return (start, end, null);
    }

    private static DateTime? ParsePersianDate(string persianDate)
    {
        // Expected format: yyyy/MM/dd
        var parts = persianDate.Trim().Split('/');
        if (parts.Length != 3) return null;
        if (!int.TryParse(parts[0], out int y) ||
            !int.TryParse(parts[1], out int m) ||
            !int.TryParse(parts[2], out int d)) return null;
        try
        {
            return Domain.Common.PersianCalendar.FromPersianDate(y, m, d);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses the TransactionDateRaw of each row and filters to the requested range.
    /// Format: "1405/04/05 14:41:48"
    /// </summary>
    private static List<BankStatementRowDto> ApplyDateFilter(
        List<BankStatementRowDto> rows,
        DateTime? start, DateTime? end)
    {
        if (start is null && end is null) return rows;

        var result = new List<BankStatementRowDto>(rows.Count);
        foreach (var row in rows)
        {
            var txDate = ParseTransactionDate(row.TransactionDateRaw);
            if (txDate is null) continue;
            if (start.HasValue && txDate.Value < start.Value) continue;
            if (end.HasValue   && txDate.Value > end.Value)   continue;
            result.Add(row);
        }
        return result;
    }

    private static DateTime? ParseTransactionDate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // "1405/04/05 14:41:48" — take the date part, ignore time for filtering
        var datePart = raw.Trim().Split(' ')[0];
        return ParsePersianDate(datePart);
    }

    /// <summary>
    /// Queries existing transactions for this account and flags rows as duplicates.
    /// Duplicate criteria: same TransactionDate + DocumentNumber + DepositAmount + WithdrawalAmount.
    /// </summary>
    private async Task MarkDuplicates(List<BankStatementRowDto> rows, int financialAccountId)
    {
        if (rows.Count == 0) return;

        // Collect candidate dates to narrow the DB query
        var candidateDates = rows
            .Select(r => ParseTransactionDate(r.TransactionDateRaw))
            .Where(d => d.HasValue)
            .Select(d => d!.Value.Date)
            .Distinct()
            .ToList();

        var existing = await _db.BankStatementTransactions
            .Where(t => t.FinancialAccountId == financialAccountId
                     && candidateDates.Contains(t.TransactionDate.Date))
            .Select(t => new
            {
                t.TransactionDate,
                t.DocumentNumber,
                t.DepositAmount,
                t.WithdrawalAmount
            })
            .ToListAsync();

        var existingSet = existing
            .Select(t => BuildDuplicateKey(
                t.TransactionDate, t.DocumentNumber, t.DepositAmount, t.WithdrawalAmount))
            .ToHashSet();

        foreach (var row in rows)
        {
            var txDate = ParseTransactionDate(row.TransactionDateRaw);
            var key    = BuildDuplicateKey(txDate ?? DateTime.MinValue, row.DocumentNumber,
                                           row.DepositAmount, row.WithdrawalAmount);
            row.IsDuplicate = existingSet.Contains(key);
        }
    }

    private static string BuildDuplicateKey(DateTime date, string? docNumber, decimal deposit, decimal withdrawal)
        => $"{date:yyyy-MM-dd}|{docNumber?.Trim()}|{deposit}|{withdrawal}";

    private static BankStatementTransaction MapToEntity(
        BankStatementRowDto row, string bankKey, int financialAccountId, string importedBy)
    {
        var txDate    = ParseTransactionDate(row.TransactionDateRaw);
        TimeSpan? time = null;

        var parts = row.TransactionDateRaw.Trim().Split(' ');
        if (parts.Length > 1 && TimeSpan.TryParse(parts[1], out var t))
            time = t;

        return new BankStatementTransaction
        {
            FinancialAccountId  = financialAccountId,
            TransactionDateRaw  = row.TransactionDateRaw,
            TransactionDate     = txDate ?? DateTime.UtcNow,
            TransactionTime     = time,
            DocumentNumber      = row.DocumentNumber,
            Description         = row.Description,
            TransactionType     = row.TransactionType,
            DepositAmount       = row.DepositAmount,
            WithdrawalAmount    = row.WithdrawalAmount,
            Balance             = row.Balance,
            ImportSource        = bankKey,
            ImportedAt          = DateTime.UtcNow,
            CreatedBy           = importedBy,
        };
    }
}

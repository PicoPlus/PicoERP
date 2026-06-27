using ClosedXML.Excel;
using PicoERP.Application.DTOs;
using PicoERP.Application.Interfaces;

namespace PicoERP.Infrastructure.Services;

/// <summary>
/// Parses an official Mellat Bank (بانک ملت) Excel statement.
///
/// File structure:
///   Rows 1–N  : Account summary / header block (not transactions).
///   One row   : Transaction table header — the row whose first non-empty cell
///               contains the word "ردیف" (row index column).
///   Remaining : Transaction data rows until end of sheet.
/// </summary>
public sealed class MelatBankStatementParser : IBankStatementParser
{
    public string BankKey => "MelatBank";
    public string DisplayName => "بانک ملت";

    // Column header names as they appear in the Excel (trimmed, exact match).
    private const string ColRow = "ردیف";
    private const string ColDate = "تاریخ";
    private const string ColDocument = "شماره سند";
    private const string ColDescription = "شرح سند";
    private const string ColType = "نوع تراکنش";
    private const string ColDeposit = "واریز (ریال)";
    private const string ColWithdrawal = "برداشت (ریال)";
    private const string ColBalance = "مانده (ریال)";

    public Task<List<BankStatementRowDto>> ParseAsync(byte[] fileBytes)
    {
        var rows = new List<BankStatementRowDto>();

        using var ms = new MemoryStream(fileBytes);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheets.First();

        // ── Step 1: locate the transaction header row ──────────────────────────
        int headerRow = FindTransactionHeaderRow(ws);
        if (headerRow < 0)
            throw new InvalidOperationException(
                "ستون‌های جدول تراکنش پیدا نشد. لطفاً از صحت فرمت فایل اطمینان حاصل فرمایید.");

        // ── Step 2: build a column-index map from the header row ───────────────
        var colMap = BuildColumnMap(ws, headerRow);

        ValidateRequiredColumns(colMap);

        // ── Step 3: read data rows ─────────────────────────────────────────────
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow;

        for (int r = headerRow + 1; r <= lastRow; r++)
        {
            var excelRow = ws.Row(r);

            // Skip entirely blank rows
            if (IsBlankRow(excelRow, colMap)) continue;

            string dateRaw = GetText(excelRow, colMap, ColDate);
            if (string.IsNullOrWhiteSpace(dateRaw)) continue; // guard: no date = skip

            var dto = new BankStatementRowDto
            {
                TransactionDateRaw = dateRaw,
                DocumentNumber     = GetText(excelRow, colMap, ColDocument),
                Description        = GetText(excelRow, colMap, ColDescription),
                TransactionType    = GetText(excelRow, colMap, ColType),
                DepositAmount      = ParseAmount(GetText(excelRow, colMap, ColDeposit)),
                WithdrawalAmount   = ParseAmount(GetText(excelRow, colMap, ColWithdrawal)),
                Balance            = ParseAmount(GetText(excelRow, colMap, ColBalance)),
            };

            rows.Add(dto);
        }

        return Task.FromResult(rows);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static int FindTransactionHeaderRow(IXLWorksheet ws)
    {
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (int r = 1; r <= lastRow; r++)
        {
            foreach (var cell in ws.Row(r).CellsUsed())
            {
                if (cell.GetString().Trim() == ColRow)
                    return r;
            }
        }
        return -1;
    }

    private static Dictionary<string, int> BuildColumnMap(IXLWorksheet ws, int headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var cell in ws.Row(headerRow).CellsUsed())
        {
            string text = cell.GetString().Trim();
            if (!string.IsNullOrEmpty(text) && !map.ContainsKey(text))
                map[text] = cell.Address.ColumnNumber;
        }
        return map;
    }

    private static void ValidateRequiredColumns(Dictionary<string, int> map)
    {
        string[] required = [ColDate, ColDocument, ColDescription, ColType, ColDeposit, ColWithdrawal, ColBalance];
        var missing = required.Where(c => !map.ContainsKey(c)).ToList();
        if (missing.Any())
            throw new InvalidOperationException(
                $"ستون‌های ضروری در فایل یافت نشد: {string.Join(", ", missing)}");
    }

    private static bool IsBlankRow(IXLRow row, Dictionary<string, int> colMap)
    {
        foreach (var col in colMap.Values)
        {
            if (!string.IsNullOrWhiteSpace(row.Cell(col).GetString()))
                return false;
        }
        return true;
    }

    private static string GetText(IXLRow row, Dictionary<string, int> colMap, string colName)
    {
        if (!colMap.TryGetValue(colName, out int col)) return string.Empty;
        return row.Cell(col).GetString().Trim();
    }

    /// <summary>Strips commas then converts to decimal. Returns 0 on failure.</summary>
    private static decimal ParseAmount(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0m;
        string clean = raw.Replace(",", "").Trim();
        return decimal.TryParse(clean, out decimal result) ? result : 0m;
    }
}

using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PicoERP.Application.Common;
using PicoERP.Application.Interfaces;
using PicoERP.Domain.Common;
using PicoERP.Infrastructure.Persistence;

namespace PicoERP.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _db;
    private readonly ISmsService _sms;

    public ReportService(AppDbContext db, ISmsService sms)
    {
        _db = db;
        _sms = sms;
    }

    public async Task<byte[]> GenerateIncomePdfAsync(DateTime from, DateTime to)
    {
        var items = await _db.Incomes.AsNoTracking()
            .Include(i => i.Category).Include(i => i.FinancialAccount)
            .Where(i => i.Date >= from && i.Date <= to)
            .OrderByDescending(i => i.Date)
            .Select(i => new Application.DTOs.IncomeDto
            {
                Id = i.Id, CategoryId = i.CategoryId, CategoryName = i.Category!.Name,
                Amount = i.Amount, Date = i.Date, Description = i.Description,
                RegisteredBy = i.RegisteredBy, CreatedAt = i.CreatedAt
            }).ToListAsync();
        return PdfGenerator.GenerateIncomeReport(items, from, to);
    }

    public async Task<byte[]> GenerateExpensePdfAsync(DateTime from, DateTime to)
    {
        var items = await _db.Expenses.AsNoTracking()
            .Include(e => e.Category)
            .Where(e => e.Date >= from && e.Date <= to)
            .OrderByDescending(e => e.Date)
            .Select(e => new Application.DTOs.ExpenseDto
            {
                Id = e.Id, CategoryId = e.CategoryId, CategoryName = e.Category!.Name,
                Group = e.Group, Amount = e.Amount, Date = e.Date,
                Description = e.Description, RegisteredBy = e.RegisteredBy, CreatedAt = e.CreatedAt
            }).ToListAsync();
        return PdfGenerator.GenerateExpenseReport(items, from, to);
    }

    public async Task<byte[]> GenerateProfitPdfAsync(DateTime from, DateTime to)
    {
        var incomeItems = await _db.Incomes.AsNoTracking()
            .Include(i => i.Category)
            .Where(i => i.Date >= from && i.Date <= to)
            .Select(i => new Application.DTOs.IncomeDto
            {
                Id = i.Id, CategoryId = i.CategoryId, CategoryName = i.Category!.Name,
                Amount = i.Amount, Date = i.Date, Description = i.Description
            }).ToListAsync();
        return PdfGenerator.GenerateIncomeReport(incomeItems, from, to);
    }

    public async Task<byte[]> GenerateSalaryPdfAsync(DateTime from, DateTime to)
    {
        var items = await _db.SalaryPayments.AsNoTracking()
            .Include(s => s.Employee)
            .Where(s => s.PeriodFrom >= from && s.PeriodTo <= to)
            .Select(s => new Application.DTOs.SalaryPaymentDto
            {
                Id = s.Id, EmployeeId = s.EmployeeId,
                EmployeeName = $"{s.Employee!.FirstName} {s.Employee.LastName}",
                Position = s.Employee.Position,
                PeriodFrom = s.PeriodFrom, PeriodTo = s.PeriodTo,
                BaseSalary = s.BaseSalary, Overtime = s.Overtime,
                NetSalary = s.NetSalary, IsPaid = s.IsPaid, PaidAt = s.PaidAt
            }).ToListAsync();

        if (!items.Any()) return Array.Empty<byte>();
        return PdfGenerator.GenerateSalarySlip(items.First());
    }

    public async Task<byte[]> GenerateIncomeExcelAsync(DateTime from, DateTime to)
    {
        var items = await _db.Incomes.AsNoTracking()
            .Include(i => i.Category).Include(i => i.FinancialAccount)
            .Where(i => i.Date >= from && i.Date <= to)
            .OrderByDescending(i => i.Date).ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("گزارش درآمد");
        ws.RightToLeft = true;

        var headers = new[] { "ردیف", "تاریخ", "دسته‌بندی", "توضیح", "حساب", "مبلغ" };
        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(1, c + 1).Style.Font.Bold = true;
            ws.Cell(1, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1976D2");
            ws.Cell(1, c + 1).Style.Font.FontColor = XLColor.White;
            ws.Cell(1, c + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        int row = 2;
        foreach (var item in items)
        {
            ws.Cell(row, 1).Value = row - 1;
            ws.Cell(row, 2).Value = PersianCalendar.ToPersianDate(item.Date);
            ws.Cell(row, 3).Value = item.Category?.Name ?? "";
            ws.Cell(row, 4).Value = item.Description ?? "";
            ws.Cell(row, 5).Value = item.FinancialAccount?.Name ?? "";
            ws.Cell(row, 6).Value = (double)item.Amount;
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
            if (row % 2 == 0) ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#E3F2FD");
            row++;
        }

        // Total
        ws.Cell(row, 5).Value = "جمع کل:";
        ws.Cell(row, 5).Style.Font.Bold = true;
        ws.Cell(row, 6).FormulaA1 = $"SUM(F2:F{row - 1})";
        ws.Cell(row, 6).Style.Font.Bold = true;
        ws.Cell(row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#BBDEFB");

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> GenerateExpenseExcelAsync(DateTime from, DateTime to)
    {
        var items = await _db.Expenses.AsNoTracking()
            .Include(e => e.Category).Include(e => e.FinancialAccount)
            .Where(e => e.Date >= from && e.Date <= to)
            .OrderByDescending(e => e.Date).ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("گزارش هزینه");
        ws.RightToLeft = true;

        var headers = new[] { "ردیف", "تاریخ", "دسته‌بندی", "توضیح", "حساب", "مبلغ" };
        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(1, c + 1).Style.Font.Bold = true;
            ws.Cell(1, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#D32F2F");
            ws.Cell(1, c + 1).Style.Font.FontColor = XLColor.White;
        }

        int row = 2;
        foreach (var item in items)
        {
            ws.Cell(row, 1).Value = row - 1;
            ws.Cell(row, 2).Value = PersianCalendar.ToPersianDate(item.Date);
            ws.Cell(row, 3).Value = item.Category?.Name ?? "";
            ws.Cell(row, 4).Value = item.Description ?? "";
            ws.Cell(row, 5).Value = item.FinancialAccount?.Name ?? "";
            ws.Cell(row, 6).Value = (double)item.Amount;
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
            if (row % 2 == 0) ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFEBEE");
            row++;
        }

        ws.Cell(row, 5).Value = "جمع کل:";
        ws.Cell(row, 5).Style.Font.Bold = true;
        ws.Cell(row, 6).FormulaA1 = $"SUM(F2:F{row - 1})";
        ws.Cell(row, 6).Style.Font.Bold = true;
        ws.Cell(row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFCDD2");

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> GenerateProfitExcelAsync(DateTime from, DateTime to)
    {
        var totalIncome = await _db.Incomes.Where(i => i.Date >= from && i.Date <= to).SumAsync(i => (decimal?)i.Amount) ?? 0;
        var totalExpense = await _db.Expenses.Where(e => e.Date >= from && e.Date <= to).SumAsync(e => (decimal?)e.Amount) ?? 0;

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("گزارش سود");
        ws.RightToLeft = true;

        ws.Cell(1, 1).Value = "شرح";
        ws.Cell(1, 2).Value = "مبلغ";
        ws.Row(1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = "جمع درآمد";
        ws.Cell(2, 2).Value = (double)totalIncome;
        ws.Cell(3, 1).Value = "جمع هزینه";
        ws.Cell(3, 2).Value = (double)totalExpense;
        ws.Cell(4, 1).Value = "سود خالص";
        ws.Cell(4, 1).Style.Font.Bold = true;
        ws.Cell(4, 2).Value = (double)(totalIncome - totalExpense);
        ws.Cell(4, 2).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── SMS report ────────────────────────────────────────────────────────

    public async Task<Result> SendReportSmsAsync(string reportType, DateTime from, DateTime to, string adminPhone)
    {
        if (string.IsNullOrWhiteSpace(adminPhone))
            return Result.Failure("شماره تلفن مدیر وارد نشده است.");

        var fromPersian = PersianCalendar.ToPersianDate(from);
        var toPersian   = PersianCalendar.ToPersianDate(to);

        string text = reportType switch
        {
            "income" => await BuildIncomeSmsAsync(from, to, fromPersian, toPersian),
            "expense" => await BuildExpenseSmsAsync(from, to, fromPersian, toPersian),
            "profit" => await BuildProfitSmsAsync(from, to, fromPersian, toPersian),
            "salary" => await BuildSalarySmsAsync(from, to, fromPersian, toPersian),
            _ => await BuildProfitSmsAsync(from, to, fromPersian, toPersian)
        };

        return await _sms.SendAsync(adminPhone, text);
    }

    private async Task<string> BuildIncomeSmsAsync(DateTime from, DateTime to, string fromPersian, string toPersian)
    {
        var total = await _db.Incomes.Where(i => i.Date >= from && i.Date <= to)
                              .SumAsync(i => (decimal?)i.Amount) ?? 0;
        var count = await _db.Incomes.CountAsync(i => i.Date >= from && i.Date <= to);
        return $"پیکو ERP - گزارش درآمد\nدوره: {fromPersian} تا {toPersian}\nتعداد تراکنش: {count}\nجمع درآمد: {total:N0} تومان";
    }

    private async Task<string> BuildExpenseSmsAsync(DateTime from, DateTime to, string fromPersian, string toPersian)
    {
        var total = await _db.Expenses.Where(e => e.Date >= from && e.Date <= to)
                              .SumAsync(e => (decimal?)e.Amount) ?? 0;
        var count = await _db.Expenses.CountAsync(e => e.Date >= from && e.Date <= to);
        return $"پیکو ERP - گزارش هزینه\nدوره: {fromPersian} تا {toPersian}\nتعداد تراکنش: {count}\nجمع هزینه: {total:N0} تومان";
    }

    private async Task<string> BuildProfitSmsAsync(DateTime from, DateTime to, string fromPersian, string toPersian)
    {
        var totalIncome  = await _db.Incomes.Where(i => i.Date >= from && i.Date <= to).SumAsync(i => (decimal?)i.Amount) ?? 0;
        var totalExpense = await _db.Expenses.Where(e => e.Date >= from && e.Date <= to).SumAsync(e => (decimal?)e.Amount) ?? 0;
        var profit = totalIncome - totalExpense;
        return $"پیکو ERP - گزارش سود\nدوره: {fromPersian} تا {toPersian}\nدرآمد: {totalIncome:N0} تومان\nهزینه: {totalExpense:N0} تومان\nسود خالص: {profit:N0} تومان";
    }

    private async Task<string> BuildSalarySmsAsync(DateTime from, DateTime to, string fromPersian, string toPersian)
    {
        var total = await _db.SalaryPayments.Where(s => s.PeriodFrom >= from && s.PeriodTo <= to)
                              .SumAsync(s => (decimal?)s.NetSalary) ?? 0;
        var count = await _db.SalaryPayments.CountAsync(s => s.PeriodFrom >= from && s.PeriodTo <= to);
        return $"پیکو ERP - گزارش حقوق\nدوره: {fromPersian} تا {toPersian}\nتعداد پرداخت: {count}\nجمع حقوق: {total:N0} تومان";
    }
}

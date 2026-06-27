using PicoERP.Application.DTOs;
using PicoERP.Domain.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PicoERP.Infrastructure.Services;

/// <summary>
/// Generates professional RTL Persian PDF documents using QuestPDF
/// </summary>
public static class PdfGenerator
{
    static PdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] GenerateSalarySlip(SalaryPaymentDto slip)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontFamily("Vazirmatn").FontSize(11).DirectionFromRightToLeft());

                page.Header().Element(c => BuildSlipHeader(c, slip));
                page.Content().Element(c => BuildSlipContent(c, slip));
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("پیکو ERP — فیش حقوقی").FontSize(9).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf();
    }

    private static void BuildSlipHeader(IContainer c, SalaryPaymentDto slip)
    {
        c.Column(col =>
        {
            col.Item().AlignCenter().Text("فیش حقوقی").Bold().FontSize(18);
            col.Item().AlignCenter().Text("سیستم مدیریت کافه نت").FontSize(12).FontColor(Colors.Grey.Darken2);
            col.Item().Height(8);
            col.Item().LineHorizontal(1).LineColor(Colors.Blue.Medium);
            col.Item().Height(4);
        });
    }

    private static void BuildSlipContent(IContainer c, SalaryPaymentDto slip)
    {
        string fromDate = PersianCalendar.ToPersianDate(slip.PeriodFrom);
        string toDate = PersianCalendar.ToPersianDate(slip.PeriodTo);

        c.Column(col =>
        {
            // Employee info
            col.Item().Height(8);
            col.Item().Background(Colors.Blue.Lighten5).Padding(8).Row(row =>
            {
                row.RelativeItem().Column(inner =>
                {
                    inner.Item().Text($"نام: {slip.EmployeeName}").Bold();
                    inner.Item().Text($"سمت: {slip.Position}");
                    inner.Item().Text($"دوره: {fromDate} تا {toDate}");
                });
            });

            col.Item().Height(12);

            // Salary details table
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(2);
                });

                void HeaderCell(string text) =>
                    table.Cell().Background(Colors.Blue.Medium).Padding(6)
                         .Text(text).FontColor(Colors.White).Bold().FontSize(10);

                HeaderCell("شرح");
                HeaderCell("مبلغ (تومان)");

                void DataRow(string label, decimal amount, bool highlight = false)
                {
                    var bg = highlight ? Colors.Green.Lighten5 : Colors.White;
                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(label).FontSize(10);
                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight()
                         .Text(PersianNumberFormatter.FormatCurrencyToman(amount)).FontSize(10);
                }

                DataRow("حقوق پایه", slip.BaseSalary);
                DataRow("اضافه کاری", slip.Overtime);
                DataRow("پاداش", slip.Bonus);

                // Deductions with red
                table.Cell().Padding(6).Text("مساعده").FontSize(10).FontColor(Colors.Red.Medium);
                table.Cell().Padding(6).AlignRight().Text($"({PersianNumberFormatter.FormatCurrencyToman(slip.Advance)})").FontSize(10).FontColor(Colors.Red.Medium);

                table.Cell().Padding(6).Text("بیمه").FontSize(10).FontColor(Colors.Red.Medium);
                table.Cell().Padding(6).AlignRight().Text($"({PersianNumberFormatter.FormatCurrencyToman(slip.Insurance)})").FontSize(10).FontColor(Colors.Red.Medium);

                table.Cell().Padding(6).Text("مالیات").FontSize(10).FontColor(Colors.Red.Medium);
                table.Cell().Padding(6).AlignRight().Text($"({PersianNumberFormatter.FormatCurrencyToman(slip.Tax)})").FontSize(10).FontColor(Colors.Red.Medium);

                table.Cell().Padding(6).Text("جریمه").FontSize(10).FontColor(Colors.Red.Medium);
                table.Cell().Padding(6).AlignRight().Text($"({PersianNumberFormatter.FormatCurrencyToman(slip.Fine)})").FontSize(10).FontColor(Colors.Red.Medium);

                // Net salary
                table.Cell().Background(Colors.Green.Lighten3).Padding(6).Text("خالص حقوق دریافتی").Bold().FontSize(11);
                table.Cell().Background(Colors.Green.Lighten3).Padding(6).AlignRight()
                     .Text(PersianNumberFormatter.FormatCurrencyToman(slip.NetSalary)).Bold().FontSize(11);
            });

            col.Item().Height(16);
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(inner =>
                {
                    inner.Item().Text("وضعیت پرداخت: " + (slip.IsPaid ? "پرداخت شده" : "در انتظار پرداخت"))
                         .FontColor(slip.IsPaid ? Colors.Green.Darken2 : Colors.Orange.Darken2).Bold();
                    if (slip.PaidAt.HasValue)
                        inner.Item().Text("تاریخ پرداخت: " + PersianCalendar.ToPersianDate(slip.PaidAt.Value));
                });
            });

            if (!string.IsNullOrWhiteSpace(slip.Notes))
            {
                col.Item().Height(8);
                col.Item().Text("توضیحات: " + slip.Notes).FontColor(Colors.Grey.Darken1).FontSize(9);
            }

            col.Item().Height(24);
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(sig =>
                {
                    sig.Item().Text("امضا کارمند:").FontSize(10);
                    sig.Item().Height(30);
                    sig.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                });
                row.ConstantItem(40);
                row.RelativeItem().Column(sig =>
                {
                    sig.Item().Text("مهر و امضا مدیریت:").FontSize(10);
                    sig.Item().Height(30);
                    sig.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                });
            });
        });
    }

    public static byte[] GenerateIncomeReport(List<IncomeDto> items, DateTime from, DateTime to)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontFamily("Vazirmatn").FontSize(10).DirectionFromRightToLeft());

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text("گزارش درآمد").Bold().FontSize(16);
                    col.Item().AlignCenter().Text($"از {PersianCalendar.ToPersianDate(from)} تا {PersianCalendar.ToPersianDate(to)}").FontSize(11).FontColor(Colors.Grey.Darken2);
                    col.Item().Height(6);
                    col.Item().LineHorizontal(1).LineColor(Colors.Blue.Medium);
                    col.Item().Height(4);
                });

                page.Content().Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(30);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(2);
                        });

                        foreach (var h in new[] { "ردیف", "تاریخ", "دسته‌بندی", "توضیح", "مبلغ" })
                            table.Cell().Background(Colors.Blue.Medium).Padding(5).Text(h).FontColor(Colors.White).Bold().FontSize(9);

                        int i = 1;
                        foreach (var item in items)
                        {
                            var bg = i % 2 == 0 ? Colors.Blue.Lighten5 : Colors.White;
                            table.Cell().Background(bg).Padding(4).AlignCenter().Text(PersianNumberFormatter.ToPersian(i.ToString())).FontSize(9);
                            table.Cell().Background(bg).Padding(4).Text(PersianCalendar.ToPersianDate(item.Date)).FontSize(9);
                            table.Cell().Background(bg).Padding(4).Text(item.CategoryName).FontSize(9);
                            table.Cell().Background(bg).Padding(4).Text(item.Description ?? "-").FontSize(9);
                            table.Cell().Background(bg).Padding(4).AlignRight().Text(PersianNumberFormatter.FormatCurrencyToman(item.Amount)).FontSize(9);
                            i++;
                        }

                        // Total row
                        decimal total = items.Sum(x => x.Amount);
                        table.Cell().ColumnSpan(4).Background(Colors.Blue.Lighten4).Padding(5).Text("جمع کل:").Bold();
                        table.Cell().Background(Colors.Blue.Lighten4).Padding(5).AlignRight().Text(PersianNumberFormatter.FormatCurrencyToman(total)).Bold();
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span($"تاریخ چاپ: {PersianCalendar.ToPersianDate(DateTime.Now)} — ").FontSize(8).FontColor(Colors.Grey.Medium);
                    t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                    t.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                    t.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf();
    }

    public static byte[] GenerateExpenseReport(List<ExpenseDto> items, DateTime from, DateTime to)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontFamily("Vazirmatn").FontSize(10).DirectionFromRightToLeft());

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text("گزارش هزینه").Bold().FontSize(16);
                    col.Item().AlignCenter().Text($"از {PersianCalendar.ToPersianDate(from)} تا {PersianCalendar.ToPersianDate(to)}").FontSize(11).FontColor(Colors.Grey.Darken2);
                    col.Item().Height(6);
                    col.Item().LineHorizontal(1).LineColor(Colors.Red.Medium);
                    col.Item().Height(4);
                });

                page.Content().Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(30);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(2);
                        });

                        foreach (var h in new[] { "ردیف", "تاریخ", "دسته‌بندی", "توضیح", "مبلغ" })
                            table.Cell().Background(Colors.Red.Medium).Padding(5).Text(h).FontColor(Colors.White).Bold().FontSize(9);

                        int i = 1;
                        foreach (var item in items)
                        {
                            var bg = i % 2 == 0 ? Colors.Red.Lighten5 : Colors.White;
                            table.Cell().Background(bg).Padding(4).AlignCenter().Text(PersianNumberFormatter.ToPersian(i.ToString())).FontSize(9);
                            table.Cell().Background(bg).Padding(4).Text(PersianCalendar.ToPersianDate(item.Date)).FontSize(9);
                            table.Cell().Background(bg).Padding(4).Text(item.CategoryName).FontSize(9);
                            table.Cell().Background(bg).Padding(4).Text(item.Description ?? "-").FontSize(9);
                            table.Cell().Background(bg).Padding(4).AlignRight().Text(PersianNumberFormatter.FormatCurrencyToman(item.Amount)).FontSize(9);
                            i++;
                        }

                        decimal total = items.Sum(x => x.Amount);
                        table.Cell().ColumnSpan(4).Background(Colors.Red.Lighten4).Padding(5).Text("جمع کل:").Bold();
                        table.Cell().Background(Colors.Red.Lighten4).Padding(5).AlignRight().Text(PersianNumberFormatter.FormatCurrencyToman(total)).Bold();
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span($"تاریخ چاپ: {PersianCalendar.ToPersianDate(DateTime.Now)} — ").FontSize(8).FontColor(Colors.Grey.Medium);
                    t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                    t.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                    t.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf();
    }
}

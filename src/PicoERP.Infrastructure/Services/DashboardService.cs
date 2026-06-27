using Microsoft.EntityFrameworkCore;
using PicoERP.Application.Common;
using PicoERP.Application.DTOs;
using PicoERP.Application.Interfaces;
using PicoERP.Domain.Common;
using PicoERP.Infrastructure.Persistence;

namespace PicoERP.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;
    public DashboardService(AppDbContext db) => _db = db;

    public async Task<DashboardDto> GetDashboardDataAsync()
    {
        var now = DateTime.Now;
        var today = now.Date;
        var todayEnd = today.AddDays(1).AddTicks(-1);

        // Persian calendar week/month/year boundaries
        int pYear = PersianCalendar.GetPersianYear(now);
        int pMonth = PersianCalendar.GetPersianMonth(now);
        var monthStart = PersianCalendar.GetPersianMonthStart(pYear, pMonth);
        var monthEnd = PersianCalendar.GetPersianMonthEnd(pYear, pMonth);
        var yearStart = PersianCalendar.GetPersianYearStart(pYear);
        var yearEnd = PersianCalendar.GetPersianYearEnd(pYear);

        // Week start (Saturday in Persian calendar)
        int dayOfWeek = (int)now.DayOfWeek;
        // Saturday = 6 in DayOfWeek, we want week to start on Saturday
        int daysFromSat = ((dayOfWeek - 6 + 7) % 7);
        var weekStart = today.AddDays(-daysFromSat);

        var todayIncome = await _db.Incomes.Where(i => i.Date >= today && i.Date <= todayEnd).SumAsync(i => (decimal?)i.Amount) ?? 0;
        var todayExpense = await _db.Expenses.Where(e => e.Date >= today && e.Date <= todayEnd).SumAsync(e => (decimal?)e.Amount) ?? 0;
        var weekIncome = await _db.Incomes.Where(i => i.Date >= weekStart && i.Date <= todayEnd).SumAsync(i => (decimal?)i.Amount) ?? 0;
        var monthIncome = await _db.Incomes.Where(i => i.Date >= monthStart && i.Date <= monthEnd).SumAsync(i => (decimal?)i.Amount) ?? 0;
        var monthExpense = await _db.Expenses.Where(e => e.Date >= monthStart && e.Date <= monthEnd).SumAsync(e => (decimal?)e.Amount) ?? 0;
        var yearIncome = await _db.Incomes.Where(i => i.Date >= yearStart && i.Date <= yearEnd).SumAsync(i => (decimal?)i.Amount) ?? 0;
        var yearExpense = await _db.Expenses.Where(e => e.Date >= yearStart && e.Date <= yearEnd).SumAsync(e => (decimal?)e.Amount) ?? 0;

        var accounts = await _db.FinancialAccounts.AsNoTracking().Where(a => a.IsActive).ToListAsync();
        var cashBalance = accounts.Where(a => a.Type == Domain.Enums.AccountType.Cash).Sum(a => a.CurrentBalance);
        var bankBalance = accounts.Where(a => a.Type == Domain.Enums.AccountType.Bank).Sum(a => a.CurrentBalance);
        var cardBalance = accounts.Where(a => a.Type == Domain.Enums.AccountType.CardReader).Sum(a => a.CurrentBalance);

        var totalEmployees = await _db.Employees.CountAsync(e => e.Status == Domain.Enums.EmployeeStatus.Active);
        var presentToday = await _db.AttendanceRecords.CountAsync(a =>
            a.Date.Date == today &&
            (a.Type == Domain.Enums.AttendanceType.Present || a.Type == Domain.Enums.AttendanceType.Overtime));
        // Unpaid salaries = total NetSalary on unpaid records
        //   minus any expense amounts already recorded under salary-related categories
        //   ("حقوق" or "هزینه کارمند") which represent partial or advance payments already made.
        var unpaidSalaries = await _db.SalaryPayments
            .Where(s => !s.IsPaid && !s.IsDeleted)
            .SumAsync(s => (decimal?)s.NetSalary) ?? 0;

        var salaryPaidExpenses = await _db.Expenses
            .Where(e => !e.IsDeleted &&
                        e.Group == Domain.Enums.ExpenseGroup.Employee &&
                        (e.Category!.Name == "حقوق" || e.Category!.Name == "هزینه کارمند"))
            .SumAsync(e => (decimal?)e.Amount) ?? 0;

        unpaidSalaries = Math.Max(0, unpaidSalaries - salaryPaidExpenses);

        // Monthly chart - last 6 Persian months
        var monthlyIncomeChart = new List<MonthlyChartDto>();
        var monthlyExpenseChart = new List<MonthlyChartDto>();
        for (int i = 5; i >= 0; i--)
        {
            int targetMonth = pMonth - i;
            int targetYear = pYear;
            while (targetMonth <= 0) { targetMonth += 12; targetYear--; }
            var mStart = PersianCalendar.GetPersianMonthStart(targetYear, targetMonth);
            var mEnd = PersianCalendar.GetPersianMonthEnd(targetYear, targetMonth);
            var mIncome = await _db.Incomes.Where(x => x.Date >= mStart && x.Date <= mEnd).SumAsync(x => (decimal?)x.Amount) ?? 0;
            var mExpense = await _db.Expenses.Where(x => x.Date >= mStart && x.Date <= mEnd).SumAsync(x => (decimal?)x.Amount) ?? 0;
            var monthName = PersianCalendar.GetPersianMonthName(targetMonth);
            monthlyIncomeChart.Add(new MonthlyChartDto { Month = monthName, MonthNumber = targetMonth, Amount = mIncome });
            monthlyExpenseChart.Add(new MonthlyChartDto { Month = monthName, MonthNumber = targetMonth, Amount = mExpense });
        }

        // Top categories
        var topIncomeCategories = (await _db.Incomes.AsNoTracking()
            .Include(i => i.Category)
            .Where(i => i.Date >= monthStart && i.Date <= monthEnd)
            .GroupBy(i => new { i.CategoryId, i.Category!.Name })
            .Select(g => new CategorySummaryDto { Name = g.Key.Name, Amount = g.Sum(x => x.Amount) })
            .ToListAsync())
            .OrderByDescending(c => c.Amount).Take(5).ToList();

        var topExpenseCategories = (await _db.Expenses.AsNoTracking()
            .Include(e => e.Category)
            .Where(e => e.Date >= monthStart && e.Date <= monthEnd)
            .GroupBy(e => new { e.CategoryId, e.Category!.Name })
            .Select(g => new CategorySummaryDto { Name = g.Key.Name, Amount = g.Sum(x => x.Amount) })
            .ToListAsync())
            .OrderByDescending(c => c.Amount).Take(5).ToList();

        // Recent transactions
        var recentIncomes = await _db.Incomes.AsNoTracking()
            .Include(i => i.Category).Include(i => i.FinancialAccount)
            .OrderByDescending(i => i.Date).ThenByDescending(i => i.CreatedAt)
            .Take(5)
            .Select(i => new IncomeDto
            {
                Id = i.Id, CategoryId = i.CategoryId, CategoryName = i.Category!.Name,
                Amount = i.Amount, Date = i.Date, Description = i.Description,
                FinancialAccountId = i.FinancialAccountId, AccountName = i.FinancialAccount != null ? i.FinancialAccount.Name : null,
                RegisteredBy = i.RegisteredBy, CreatedAt = i.CreatedAt
            }).ToListAsync();

        var recentExpenses = await _db.Expenses.AsNoTracking()
            .Include(e => e.Category).Include(e => e.FinancialAccount)
            .OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt)
            .Take(5)
            .Select(e => new ExpenseDto
            {
                Id = e.Id, CategoryId = e.CategoryId, CategoryName = e.Category!.Name,
                Group = e.Group, Amount = e.Amount, Date = e.Date,
                Description = e.Description,
                FinancialAccountId = e.FinancialAccountId, AccountName = e.FinancialAccount != null ? e.FinancialAccount.Name : null,
                RegisteredBy = e.RegisteredBy, CreatedAt = e.CreatedAt
            }).ToListAsync();

        return new DashboardDto
        {
            TodayIncome = todayIncome,
            TodayExpense = todayExpense,
            TodayProfit = todayIncome - todayExpense,
            WeekIncome = weekIncome,
            MonthIncome = monthIncome,
            MonthExpense = monthExpense,
            MonthProfit = monthIncome - monthExpense,
            YearIncome = yearIncome,
            YearExpense = yearExpense,
            YearProfit = yearIncome - yearExpense,
            CashBalance = cashBalance,
            BankBalance = bankBalance,
            CardReaderBalance = cardBalance,
            TotalEmployees = totalEmployees,
            PresentEmployees = presentToday,
            AbsentEmployees = totalEmployees - presentToday,
            UnpaidSalaries = unpaidSalaries,
            MonthlyIncomeChart = monthlyIncomeChart,
            MonthlyExpenseChart = monthlyExpenseChart,
            TopIncomeCategories = topIncomeCategories,
            TopExpenseCategories = topExpenseCategories,
            RecentIncomes = recentIncomes,
            RecentExpenses = recentExpenses
        };
    }
}

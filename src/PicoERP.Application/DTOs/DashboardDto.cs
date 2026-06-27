namespace PicoERP.Application.DTOs;

public class DashboardDto
{
    // امروز
    public decimal TodayIncome { get; set; }
    public decimal TodayExpense { get; set; }
    public decimal TodayProfit { get; set; }

    // این هفته
    public decimal WeekIncome { get; set; }

    // این ماه
    public decimal MonthIncome { get; set; }
    public decimal MonthExpense { get; set; }
    public decimal MonthProfit { get; set; }

    // این سال
    public decimal YearIncome { get; set; }
    public decimal YearExpense { get; set; }
    public decimal YearProfit { get; set; }

    // حساب ها
    public decimal CashBalance { get; set; }
    public decimal BankBalance { get; set; }
    public decimal CardReaderBalance { get; set; }

    // کارمندان
    public int PresentEmployees { get; set; }
    public int AbsentEmployees { get; set; }
    public int TotalEmployees { get; set; }

    // حقوق
    public decimal UnpaidSalaries { get; set; }

    // آخرین تراکنش ها
    public List<IncomeDto> RecentIncomes { get; set; } = new();
    public List<ExpenseDto> RecentExpenses { get; set; } = new();

    // نمودار ماهانه
    public List<MonthlyChartDto> MonthlyIncomeChart { get; set; } = new();
    public List<MonthlyChartDto> MonthlyExpenseChart { get; set; } = new();

    // بیشترین درآمدها
    public List<CategorySummaryDto> TopIncomeCategories { get; set; } = new();
    public List<CategorySummaryDto> TopExpenseCategories { get; set; } = new();
}

public class MonthlyChartDto
{
    public string Month { get; set; } = string.Empty;
    public int MonthNumber { get; set; }
    public decimal Amount { get; set; }
}

public class CategorySummaryDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public double Percentage { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
}

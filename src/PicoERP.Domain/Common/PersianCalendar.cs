using System.Globalization;

namespace PicoERP.Domain.Common;

/// <summary>
/// Persian (Jalali/Shamsi) calendar utilities — used everywhere dates are displayed
/// </summary>
public static class PersianCalendar
{
    private static readonly System.Globalization.PersianCalendar _cal = new();

    public static string ToPersianDate(DateTime date, string format = "yyyy/MM/dd")
    {
        int year = _cal.GetYear(date);
        int month = _cal.GetMonth(date);
        int day = _cal.GetDayOfMonth(date);
        return format
            .Replace("yyyy", year.ToString("D4"))
            .Replace("MM", month.ToString("D2"))
            .Replace("dd", day.ToString("D2"))
            .Replace("M", month.ToString())
            .Replace("d", day.ToString());
    }

    public static string ToPersianDateTime(DateTime date)
    {
        string persianDate = ToPersianDate(date);
        return $"{persianDate} {date:HH:mm}";
    }

    public static DateTime FromPersianDate(int year, int month, int day)
    {
        return _cal.ToDateTime(year, month, day, 0, 0, 0, 0);
    }

    public static (int Year, int Month, int Day) GetPersianDateParts(DateTime date)
    {
        return (_cal.GetYear(date), _cal.GetMonth(date), _cal.GetDayOfMonth(date));
    }

    public static int GetPersianYear(DateTime date) => _cal.GetYear(date);
    public static int GetPersianMonth(DateTime date) => _cal.GetMonth(date);
    public static int GetPersianDay(DateTime date) => _cal.GetDayOfMonth(date);

    public static string GetPersianMonthName(int month) => month switch
    {
        1 => "فروردین",
        2 => "اردیبهشت",
        3 => "خرداد",
        4 => "تیر",
        5 => "مرداد",
        6 => "شهریور",
        7 => "مهر",
        8 => "آبان",
        9 => "آذر",
        10 => "دی",
        11 => "بهمن",
        12 => "اسفند",
        _ => ""
    };

    public static string GetPersianDayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Saturday => "شنبه",
        DayOfWeek.Sunday => "یک‌شنبه",
        DayOfWeek.Monday => "دوشنبه",
        DayOfWeek.Tuesday => "سه‌شنبه",
        DayOfWeek.Wednesday => "چهارشنبه",
        DayOfWeek.Thursday => "پنج‌شنبه",
        DayOfWeek.Friday => "جمعه",
        _ => ""
    };

    public static DateTime GetPersianMonthStart(int year, int month)
        => _cal.ToDateTime(year, month, 1, 0, 0, 0, 0);

    public static DateTime GetPersianMonthEnd(int year, int month)
    {
        int days = _cal.GetDaysInMonth(year, month);
        return _cal.ToDateTime(year, month, days, 23, 59, 59, 999);
    }

    public static DateTime GetPersianYearStart(int year)
        => _cal.ToDateTime(year, 1, 1, 0, 0, 0, 0);

    public static DateTime GetPersianYearEnd(int year)
        => _cal.ToDateTime(year, 12, _cal.GetDaysInMonth(year, 12), 23, 59, 59, 999);
}

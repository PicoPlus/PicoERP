namespace PicoERP.Domain.Common;

/// <summary>
/// Converts between Persian and Latin digits, and formats currency values
/// </summary>
public static class PersianNumberFormatter
{
    private static readonly char[] PersianDigits = { '۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹' };
    private static readonly char[] ArabicDigits = { '٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩' };

    public static string ToPersian(string number)
    {
        var result = new System.Text.StringBuilder();
        foreach (char c in number)
        {
            if (c >= '0' && c <= '9')
                result.Append(PersianDigits[c - '0']);
            else
                result.Append(c);
        }
        return result.ToString();
    }

    public static string ToEnglish(string number)
    {
        var result = new System.Text.StringBuilder();
        foreach (char c in number)
        {
            if (c >= '۰' && c <= '۹')
                result.Append((char)('0' + (c - '۰')));
            else if (c >= '٠' && c <= '٩')
                result.Append((char)('0' + (c - '٠')));
            else
                result.Append(c);
        }
        return result.ToString();
    }

    public static string FormatCurrencyRial(decimal amount, bool usePersianDigits = true)
    {
        string formatted = amount.ToString("N0");
        return usePersianDigits
            ? $"{ToPersian(formatted)} ریال"
            : $"{formatted} ریال";
    }

    public static string FormatCurrencyToman(decimal amount, bool usePersianDigits = true)
    {
        decimal toman = amount / 10;
        string formatted = toman.ToString("N0");
        return usePersianDigits
            ? $"{ToPersian(formatted)} تومان"
            : $"{formatted} تومان";
    }

    public static string FormatNumber(decimal amount, bool usePersianDigits = true)
    {
        string formatted = amount.ToString("N0");
        return usePersianDigits ? ToPersian(formatted) : formatted;
    }

    public static string FormatDate(DateTime date, bool usePersianDigits = true)
    {
        string persian = PersianCalendar.ToPersianDate(date);
        return usePersianDigits ? ToPersian(persian) : persian;
    }
}

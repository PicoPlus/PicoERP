using PicoERP.Application.DTOs;
using PicoERP.Domain.Common;

namespace PicoERP.Web.Services;

/// <summary>
/// Manages global application state (current user, theme, settings, etc.)
/// </summary>
public class AppStateService
{
    public UserInfoDto? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser != null;
    public bool IsDarkMode { get; set; }
    public bool UsePersianNumerals { get; set; } = true;
    // Currency is fixed to Rial. Amounts in the DB are stored in Toman; multiply by 10 for display.
    public string CurrencyUnit { get; set; } = "ریال";
    public bool IsRial => true;
    public string BusinessName { get; set; } = "کافه نت پیکو";
    public string HubSpotApiKey { get; set; } = "";
    public string ZohalToken    { get; set; } = "";
    public string SmsApiKey { get; set; } = "";
    public string SmsAdminPhone { get; set; } = "";
    public string SmsSender { get; set; } = "+98200010000";

    public event Action? OnChange;

    public void SetUser(UserInfoDto? user)
    {
        CurrentUser = user;
        NotifyStateChanged();
    }

    public void Logout()
    {
        CurrentUser = null;
        NotifyStateChanged();
    }

    public void ToggleDarkMode()
    {
        IsDarkMode = !IsDarkMode;
        NotifyStateChanged();
    }

    public string FormatAmount(decimal amount)
    {
        string formatted = amount.ToString("N0");
        if (UsePersianNumerals)
            formatted = global::PicoERP.Domain.Common.PersianNumberFormatter.ToPersian(formatted);
        return $"{formatted} {CurrencyUnit}";
    }

    public string FormatDate(DateTime date)
    {
        string d = global::PicoERP.Domain.Common.PersianCalendar.ToPersianDate(date);
        return UsePersianNumerals
            ? global::PicoERP.Domain.Common.PersianNumberFormatter.ToPersian(d)
            : d;
    }

    public string FormatDateTime(DateTime date)
    {
        string d = global::PicoERP.Domain.Common.PersianCalendar.ToPersianDateTime(date);
        return UsePersianNumerals
            ? global::PicoERP.Domain.Common.PersianNumberFormatter.ToPersian(d)
            : d;
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

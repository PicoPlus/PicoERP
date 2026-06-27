using MudBlazor;

namespace PicoERP.Web.Services;

public class NotificationService
{
    private readonly ISnackbar _snackbar;
    public NotificationService(ISnackbar snackbar) => _snackbar = snackbar;

    public void Success(string message) =>
        _snackbar.Add(message, Severity.Success, c => { c.Icon = Icons.Material.Filled.CheckCircle; });

    public void Error(string message) =>
        _snackbar.Add(message, Severity.Error, c => { c.Icon = Icons.Material.Filled.Error; });

    public void Warning(string message) =>
        _snackbar.Add(message, Severity.Warning, c => { c.Icon = Icons.Material.Filled.Warning; });

    public void Info(string message) =>
        _snackbar.Add(message, Severity.Info, c => { c.Icon = Icons.Material.Filled.Info; });
}

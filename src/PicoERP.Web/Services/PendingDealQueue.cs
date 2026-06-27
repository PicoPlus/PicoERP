namespace PicoERP.Web.Services;

/// <summary>
/// Singleton in-memory queue that bridges the webhook HTTP controller
/// (which runs outside any Blazor circuit) with the Blazor UI.
///
/// The controller enqueues deal IDs; the /hubspot page subscribes to
/// <see cref="OnNewDeal"/> and refreshes its pending list.
/// </summary>
public sealed class PendingDealQueue
{
    // Fired on the thread pool whenever a new deal arrives.
    // UI components must call InvokeAsync(StateHasChanged) inside the handler.
    public event Action? OnNewDeal;

    public void Notify() => OnNewDeal?.Invoke();
}

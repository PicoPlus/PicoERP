using PicoERP.Application.DTOs;

namespace PicoERP.Application.Interfaces;

public interface IPushNotificationService
{
    Task SaveSubscriptionAsync(PushSubscriptionDto dto);
    Task RemoveSubscriptionAsync(string endpoint);
    Task SendAsync(PushNotificationDto notification);
    Task BroadcastAsync(PushNotificationDto notification);
}

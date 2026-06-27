using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PicoERP.Application.DTOs;
using PicoERP.Application.Interfaces;
using WebPush;

namespace PicoERP.Infrastructure.Services;

/// <summary>
/// Sends Web Push notifications to subscribed browsers (Windows, Android, etc.)
/// Subscriptions are kept in memory; for production, persist to the database.
/// </summary>
public sealed class PushNotificationService : IPushNotificationService
{
    private readonly WebPushClient _client;
    private readonly VapidDetails _vapid;

    // In-memory store: endpoint → subscription
    // For multi-server or restart persistence, move this to the DB.
    private static readonly ConcurrentDictionary<string, PushSubscriptionDto>
        _subscriptions = new(StringComparer.Ordinal);

    public PushNotificationService(IConfiguration config)
    {
        string subject    = config["Push:Subject"]    ?? "mailto:admin@picoerp.ir";
        string publicKey  = config["Push:PublicKey"]  ?? throw new InvalidOperationException("Push:PublicKey not configured");
        string privateKey = config["Push:PrivateKey"] ?? throw new InvalidOperationException("Push:PrivateKey not configured");

        _vapid  = new VapidDetails(subject, publicKey, privateKey);
        _client = new WebPushClient();
    }

    public Task SaveSubscriptionAsync(PushSubscriptionDto dto)
    {
        _subscriptions[dto.Endpoint] = dto;
        return Task.CompletedTask;
    }

    public Task RemoveSubscriptionAsync(string endpoint)
    {
        _subscriptions.TryRemove(endpoint, out _);
        return Task.CompletedTask;
    }

    /// <summary>Send to a specific userId's subscriptions.</summary>
    public async Task SendAsync(PushNotificationDto notification)
    {
        var targets = string.IsNullOrEmpty(notification.UserId)
            ? _subscriptions.Values
            : _subscriptions.Values.Where(s => s.UserId == notification.UserId);

        await SendToAsync(targets, notification);
    }

    /// <summary>Broadcast to every subscribed device.</summary>
    public async Task BroadcastAsync(PushNotificationDto notification)
        => await SendToAsync(_subscriptions.Values, notification);

    // ── private ──────────────────────────────────────────────────────────────

    private async Task SendToAsync(IEnumerable<PushSubscriptionDto> targets, PushNotificationDto notification)
    {
        var payload = JsonSerializer.Serialize(new
        {
            title = notification.Title,
            body  = notification.Body,
            url   = notification.Url ?? "/",
            tag   = notification.Tag ?? "picoerp",
            icon  = "/icon-192.png",
            badge = "/icon-192.png"
        });

        var dead = new List<string>();

        foreach (var sub in targets)
        {
            try
            {
                var pushSub = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await _client.SendNotificationAsync(pushSub, payload, _vapid);
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone
                                           || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Subscription expired — remove it
                dead.Add(sub.Endpoint);
            }
            catch
            {
                // Network error — keep the subscription, retry next time
            }
        }

        foreach (var ep in dead) _subscriptions.TryRemove(ep, out _);
    }
}

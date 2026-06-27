using Microsoft.AspNetCore.Mvc;
using PicoERP.Application.DTOs;
using PicoERP.Application.Interfaces;

namespace PicoERP.Web.Controllers;

/// <summary>
/// REST endpoints called from the browser JavaScript to manage push subscriptions.
/// </summary>
[ApiController]
[Route("api/push")]
public class PushController : ControllerBase
{
    private readonly IPushNotificationService _push;
    public PushController(IPushNotificationService push) => _push = push;

    /// <summary>Browser calls this after subscribing to push notifications.</summary>
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Endpoint)) return BadRequest("endpoint required");
        await _push.SaveSubscriptionAsync(dto);
        return Ok();
    }

    /// <summary>Browser calls this when the user revokes notification permission.</summary>
    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] PushSubscriptionDto dto)
    {
        await _push.RemoveSubscriptionAsync(dto.Endpoint);
        return Ok();
    }

    /// <summary>Server-side trigger: send a test notification (dev/admin use).</summary>
    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] PushNotificationDto dto)
    {
        await _push.BroadcastAsync(dto);
        return Ok();
    }
}

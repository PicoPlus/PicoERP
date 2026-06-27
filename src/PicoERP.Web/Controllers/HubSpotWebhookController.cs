using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using PicoERP.Application.DTOs;
using PicoERP.Domain.Entities;
using PicoERP.Infrastructure.Persistence;
using PicoERP.Web.Services;

namespace PicoERP.Web.Controllers;

/// <summary>
/// Receives HubSpot webhook events.
///
/// Configure in HubSpot:  Settings → Integrations → Private Apps → Webhooks
///   URL:    https://your-domain/api/hubspot/webhook
///   Events: deal.propertyChange  (property: dealstage)
///
/// HubSpot sends an array of event objects. We only act when
/// the new value of "dealstage" is one of the well-known "closedwon" values.
/// </summary>
[ApiController]
[Route("api/hubspot")]
public sealed class HubSpotWebhookController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PendingDealQueue _queue;

    // All HubSpot pipeline stage IDs that mean "Closed Won".
    // Add your own portal-specific stage IDs here or read from config.
    private static readonly HashSet<string> ClosedWonStages =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "closedwon", "closed won", "appointmentscheduled",
            // numeric stage IDs vary by portal – include yours:
            "presentationscheduled", "decisionmakerboughtin", "contractsent"
        };

    public HubSpotWebhookController(AppDbContext db, PendingDealQueue queue)
    {
        _db = db;
        _queue = queue;
    }

    // ── Verification challenge (HubSpot sends GET to verify the endpoint) ──
    [HttpGet("webhook")]
    public IActionResult Verify([FromQuery] string? challenge) =>
        challenge is not null ? Ok(challenge) : Ok();

    // ── Main event receiver ────────────────────────────────────────────────
    [HttpPost("webhook")]
    public async Task<IActionResult> Receive()
    {
        string body;
        using (var reader = new System.IO.StreamReader(Request.Body))
            body = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(body))
            return Ok();

        // HubSpot posts an array of event objects
        JsonArray? events;
        try { events = JsonNode.Parse(body)?.AsArray(); }
        catch { return BadRequest("invalid json"); }

        if (events == null) return Ok();

        bool anyQueued = false;

        foreach (var ev in events)
        {
            if (ev == null) continue;

            string? eventType   = ev["subscriptionType"]?.GetValue<string>();
            string? propertyName = ev["propertyName"]?.GetValue<string>();
            string? newValue     = ev["propertyValue"]?.GetValue<string>();
            string? objectId     = ev["objectId"]?.ToString();

            // We only care about dealstage → closedwon transitions
            if (eventType != "deal.propertyChange") continue;
            if (propertyName != "dealstage") continue;
            if (newValue == null || !ClosedWonStages.Contains(newValue)) continue;
            if (string.IsNullOrWhiteSpace(objectId)) continue;

            // Avoid duplicate pending rows for the same deal
            if (_db.PendingDeals.Any(p => p.HubSpotDealId == objectId && p.IsApproved == null))
                continue;

            // Extract optional deal properties embedded in the event (v3 enriched payload)
            string dealName   = ev["dealName"]?.GetValue<string>()
                             ?? ev["objectProperties"]?["dealname"]?.GetValue<string>()
                             ?? $"Deal {objectId}";

            decimal amount = 0m;
            if (decimal.TryParse(
                    ev["amount"]?.GetValue<string>()
                 ?? ev["objectProperties"]?["amount"]?.GetValue<string>()
                 ?? "0",
                    out var amt)) amount = amt;

            string? currency    = ev["objectProperties"]?["deal_currency_code"]?.GetValue<string>();
            string? contactName = ev["objectProperties"]?["associatedcontactname"]?.GetValue<string>();

            var pending = new PendingDeal
            {
                HubSpotDealId = objectId,
                DealName      = dealName,
                Amount        = amount,
                Currency      = currency,
                Stage         = newValue,
                ContactName   = contactName,
                RawPayload    = body,
                CreatedAt     = DateTime.UtcNow
            };

            _db.PendingDeals.Add(pending);
            anyQueued = true;
        }

        if (anyQueued)
        {
            await _db.SaveChangesAsync();
            _queue.Notify();          // wake up the Blazor UI
        }

        return Ok();
    }
}

using PicoERP.Domain.Common;

namespace PicoERP.Domain.Entities;

/// <summary>
/// Stores an incoming HubSpot "deal closed-won" webhook payload
/// waiting for an operator to approve (→ Income) or reject.
/// </summary>
public class PendingDeal : BaseEntity
{
    /// HubSpot deal object ID
    public string HubSpotDealId { get; set; } = string.Empty;

    /// Deal name from HubSpot
    public string DealName { get; set; } = string.Empty;

    /// Amount value sent by HubSpot (in the account's currency)
    public decimal Amount { get; set; }

    /// ISO currency code reported by HubSpot (e.g. "USD", "IRR")
    public string? Currency { get; set; }

    /// The pipeline stage that triggered the webhook
    public string? Stage { get; set; }

    /// Associated contact / company name (best-effort)
    public string? ContactName { get; set; }

    /// Raw JSON body saved for auditing
    public string? RawPayload { get; set; }

    /// null = pending, true = approved, false = rejected
    public bool? IsApproved { get; set; }

    /// ID of the Income record created after approval
    public int? CreatedIncomeId { get; set; }

    /// Who actioned this item
    public string? ActionedBy { get; set; }

    public DateTime? ActionedAt { get; set; }
}

using PicoERP.Domain.Common;

namespace PicoERP.Domain.Entities;

/// <summary>
/// A CRM contact imported from HubSpot (via deals or direct sync).
/// Deduplicated by HsObjectId (hs_object_id) — never creates duplicates.
/// </summary>
public class HubSpotContact : BaseEntity
{
    /// <summary>HubSpot hs_object_id — the permanent, unique contact ID.</summary>
    public string HsObjectId { get; set; } = string.Empty;

    public string? FirstName  { get; set; }
    public string? LastName   { get; set; }
    public string? Email      { get; set; }
    public string? Phone      { get; set; }
    public string? Company    { get; set; }

    /// <summary>کد ملی</summary>
    public string? NCode { get; set; }

    /// <summary>تاریخ تولد (stored as string to support Persian date formats)</summary>
    public string? BirthDate { get; set; }

    /// <summary>نام پدر</summary>
    public string? FatherName { get; set; }

    /// <summary>تلفن همراه</summary>
    public string? MobilePhone { get; set; }

    /// <summary>ISO currency or pipeline stage context when imported from a deal.</summary>
    public string? SourceDealId { get; set; }

    /// <summary>When HubSpot first created this contact.</summary>
    public DateTime? HsCreatedAt { get; set; }

    /// <summary>Last time HubSpot modified this contact.</summary>
    public DateTime? HsUpdatedAt { get; set; }
}

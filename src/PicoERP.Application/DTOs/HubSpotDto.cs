namespace PicoERP.Application.DTOs;

// ── Webhook deal payload ───────────────────────────────────────────────────

/// <summary>Parsed from a HubSpot "deal.propertyChange" webhook event.</summary>
public class HubSpotDealWebhookDto
{
    public string ObjectId { get; set; } = string.Empty;   // deal ID
    public string? DealName { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Stage { get; set; }
    public string? ContactName { get; set; }
    public string RawJson { get; set; } = string.Empty;
}

/// <summary>A deal fetched directly from the HubSpot CRM deals API.</summary>
public class HubSpotDealDto
{
    public string Id          { get; set; } = string.Empty;
    public string DealName    { get; set; } = string.Empty;
    public decimal Amount     { get; set; }
    public string? Currency   { get; set; }
    public string? Stage      { get; set; }
    public string? StageLabel { get; set; }
    public string? Description { get; set; }
    public DateTime? CloseDate { get; set; }
    public DateTime? CreateDate { get; set; }
    /// <summary>Associated contact hs_object_id (if any).</summary>
    public string? AssociatedContactId { get; set; }
    public string? ContactFirstName { get; set; }
    public string? ContactLastName  { get; set; }
    public string? ContactEmail     { get; set; }
    public string? ContactPhone     { get; set; }
    public string? ContactCompany   { get; set; }
    public List<HubSpotLineItemDto> LineItems { get; set; } = new();
}

/// <summary>Pending deal row surfaced to the UI for approval.</summary>
public class PendingDealDto
{
    public int Id { get; set; }
    public string HubSpotDealId { get; set; } = string.Empty;
    public string DealName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Stage { get; set; }
    public string? ContactName { get; set; }
    public DateTime ReceivedAt { get; set; }
}

public class HubSpotContactDto
{
    public string Id { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Company { get; set; }
    /// <summary>کد ملی</summary>
    public string? NCode { get; set; }
    /// <summary>تاریخ تولد</summary>
    public string? BirthDate { get; set; }
    /// <summary>نام پدر</summary>
    public string? FatherName { get; set; }
    /// <summary>تلفن همراه</summary>
    public string? MobilePhone { get; set; }
    public DateTime? CreateDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}

/// <summary>Form model for creating a new contact via the UI.</summary>
public class CreateHubSpotContactDto
{
    public string? NCode { get; set; }
    public string? BirthDate { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? FatherName { get; set; }
    public string? MobilePhone { get; set; }
    public string? Email { get; set; }
    public string? Company { get; set; }
}

/// <summary>Form model for creating or updating a deal.</summary>
public class CreateHubSpotDealDto
{
    public string DealName { get; set; } = string.Empty;
    public string? Stage { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public DateTime? CloseDate { get; set; }
    public string? AssociatedContactId { get; set; }
}

/// <summary>A pipeline stage fetched from HubSpot.</summary>
public class HubSpotDealStageDto
{
    public string Id    { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>Display order index.</summary>
    public int DisplayOrder { get; set; }
    /// <summary>Probability (0–1) from HubSpot metadata.</summary>
    public double? Probability { get; set; }
}

/// <summary>A note (engagement) attached to a deal or contact.</summary>
public class HubSpotNoteDto
{
    public string Id { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    /// <summary>Associated deal or contact id.</summary>
    public string? AssociatedObjectId { get; set; }
}

/// <summary>A line item (product) associated with a deal.</summary>
public class HubSpotLineItemDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public string? Sku { get; set; }
    public string? Description { get; set; }
}

/// <summary>A product from the HubSpot product catalog.</summary>
public class HubSpotProductDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public decimal Price { get; set; }
    public string? Sku { get; set; }
    public string? Description { get; set; }
}

/// <summary>A call engagement associated with a contact or deal.</summary>
public class HubSpotCallDto
{
    public string Id { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? Status { get; set; }
    public string? Direction { get; set; }
    public int? DurationSeconds { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? AssociatedObjectId { get; set; }
}

/// <summary>A file stored in HubSpot Files API.</summary>
public class HubSpotFileDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Extension { get; set; }
    public long? Size { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class HubSpotSyncResultDto
{
    public int ContactsSynced { get; set; }
    public int ContactsCreated { get; set; }
    public int ContactsUpdated { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
}

public class HubSpotSettingsDto
{
    public string ApiKey { get; set; } = string.Empty;
    public bool SyncContacts { get; set; } = true;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

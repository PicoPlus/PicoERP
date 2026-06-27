using PicoERP.Application.DTOs;
using PicoERP.Domain.Entities;

namespace PicoERP.Application.Interfaces;

public interface IHubSpotService
{
    /// <summary>Returns the API key persisted in the database, or null when not configured.</summary>
    Task<string?> GetApiKeyAsync();

    /// <summary>Persists the API key to the database (survives restarts).</summary>
    Task SaveApiKeyAsync(string apiKey);

    /// <summary>Tests the API key by fetching a single contact.</summary>
    Task<bool> TestConnectionAsync(string apiKey);

    // ── Contacts ────────────────────────────────────────────────────────────

    /// <summary>Fetches all contacts from HubSpot (paged internally).</summary>
    Task<List<HubSpotContactDto>> GetContactsAsync(string apiKey, int limit = 100);

    /// <summary>Fetches a single contact by its HubSpot object ID.</summary>
    Task<HubSpotContactDto?> GetContactByIdAsync(string apiKey, string contactId);

    /// <summary>Searches contacts by query string (name / email / phone / ncode).</summary>
    Task<List<HubSpotContactDto>> SearchContactsAsync(string apiKey, string query, int limit = 10);

    /// <summary>Creates a new contact in HubSpot and returns the created contact with its ID.</summary>
    Task<HubSpotContactDto> CreateContactAsync(string apiKey, CreateHubSpotContactDto dto);

    /// <summary>Updates an existing contact in HubSpot by hubspotId.</summary>
    Task UpdateContactAsync(string apiKey, string hubspotId, CreateHubSpotContactDto dto);

    /// <summary>Deletes a contact from HubSpot by hubspotId.</summary>
    Task DeleteContactAsync(string apiKey, string hubspotId);

    /// <summary>
    /// Checks whether a national code (ncode custom property) is already in use.
    /// Returns the existing contact's ID, or null when the ncode is free.
    /// </summary>
    Task<string?> FindContactByNcodeAsync(string apiKey, string ncode);

    /// <summary>
    /// Checks whether a mobile phone number is already in use.
    /// Returns the existing contact's ID, or null when the mobile is free.
    /// </summary>
    Task<string?> FindContactByMobileAsync(string apiKey, string mobile);

    /// <summary>
    /// Upserts a contact (keyed on HsObjectId) into the local HubSpotContacts table.
    /// Returns true when a new row was inserted, false when an existing row was updated.
    /// </summary>
    Task<bool> SaveContactLocallyAsync(HubSpotContactDto contact, string? sourceDealId = null);

    /// <summary>Push a contact to HubSpot. Creates if not exists (by email), otherwise updates.</summary>
    Task<HubSpotSyncResultDto> SyncContactAsync(string apiKey, HubSpotContactDto contact);

    /// <summary>Bulk push a list of contacts.</summary>
    Task<HubSpotSyncResultDto> SyncAllContactsAsync(string apiKey, List<HubSpotContactDto> contacts);

    // ── Deals ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches deals from HubSpot, optionally filtered by close-date range.
    /// Also resolves associated contacts and returns them inline.
    /// </summary>
    Task<List<HubSpotDealDto>> GetDealsAsync(
        string apiKey,
        DateTime? from = null,
        DateTime? to   = null,
        int limit      = 200);

    /// <summary>Fetches a single deal by ID with full properties and line items.</summary>
    Task<HubSpotDealDto?> GetDealByIdAsync(string apiKey, string dealId);

    /// <summary>Creates a new deal in HubSpot. Returns the created deal DTO.</summary>
    Task<HubSpotDealDto> CreateDealAsync(string apiKey, CreateHubSpotDealDto dto);

    /// <summary>Updates an existing deal in HubSpot.</summary>
    Task UpdateDealAsync(string apiKey, string dealId, CreateHubSpotDealDto dto);

    /// <summary>Deletes a deal from HubSpot.</summary>
    Task DeleteDealAsync(string apiKey, string dealId);

    /// <summary>Returns all deals associated with a given contact.</summary>
    Task<List<HubSpotDealDto>> GetContactDealsAsync(string apiKey, string contactId);

    // ── Pipeline stages ──────────────────────────────────────────────────────

    /// <summary>Fetches all stages from the default deals pipeline.</summary>
    Task<List<HubSpotDealStageDto>> GetDealStagesAsync(string apiKey);

    // ── Notes (engagements) ──────────────────────────────────────────────────

    /// <summary>Fetches notes associated with a deal.</summary>
    Task<List<HubSpotNoteDto>> GetDealNotesAsync(string apiKey, string dealId);

    /// <summary>Fetches notes associated with a contact.</summary>
    Task<List<HubSpotNoteDto>> GetContactNotesAsync(string apiKey, string contactId);

    /// <summary>Creates a note and associates it with either a deal or a contact.</summary>
    Task<HubSpotNoteDto> AddNoteAsync(string apiKey, string body, string? dealId, string? contactId);

    // ── Line items ───────────────────────────────────────────────────────────

    /// <summary>Fetches line items associated with a deal.</summary>
    Task<List<HubSpotLineItemDto>> GetLineItemsAsync(string apiKey, string dealId);

    /// <summary>Creates a line item and associates it with the given deal. Returns the new line item ID.</summary>
    Task<string> CreateLineItemAsync(string apiKey, string dealId, HubSpotLineItemDto item);

    /// <summary>Deletes a line item by its ID.</summary>
    Task DeleteLineItemAsync(string apiKey, string lineItemId);

    // ── Products (catalog) ───────────────────────────────────────────────────

    /// <summary>Fetches products from the HubSpot product catalog.</summary>
    Task<List<HubSpotProductDto>> GetProductsAsync(string apiKey);

    // ── Calls (engagements) ──────────────────────────────────────────────────

    /// <summary>Fetches call engagements associated with a contact.</summary>
    Task<List<HubSpotCallDto>> GetContactCallsAsync(string apiKey, string contactId);

    /// <summary>Fetches call engagements associated with a deal.</summary>
    Task<List<HubSpotCallDto>> GetDealCallsAsync(string apiKey, string dealId);

    // ── Files ────────────────────────────────────────────────────────────────

    /// <summary>Uploads a file to HubSpot Files API and returns the file metadata.</summary>
    Task<HubSpotFileDto> UploadFileAsync(string apiKey, string fileName, byte[] data, string mimeType);

    /// <summary>Fetches files attached (via notes) to a contact or deal.</summary>
    Task<List<HubSpotFileDto>> GetFilesForObjectAsync(string apiKey, string objectType, string objectId);
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PicoERP.Application.DTOs;
using PicoERP.Application.Interfaces;
using PicoERP.Domain.Entities;
using PicoERP.Infrastructure.Persistence;

namespace PicoERP.Infrastructure.Services;

/// <summary>
/// HubSpot CRM integration using the v3 Contacts / Deals / Notes / Line-Items APIs.
/// Docs: https://developers.hubspot.com/docs/api/crm/contacts
/// </summary>
public class HubSpotService : IHubSpotService
{
    private const string BaseUrl           = "https://api.hubapi.com";
    private const string SettingApiKey     = "HubSpot:ApiKey";
    // Max parallel contact-resolution calls during GetDealsAsync
    private const int    ContactFetchLimit = 8;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppDbContext       _db;
    private readonly ISettingsService   _settings;

    public HubSpotService(
        IHttpClientFactory httpClientFactory,
        AppDbContext       db,
        ISettingsService   settings)
    {
        _httpClientFactory = httpClientFactory;
        _db                = db;
        _settings          = settings;
    }

    // ── Settings helpers ────────────────────────────────────────────────────

    public async Task<string?> GetApiKeyAsync()
        => await _settings.GetAsync(SettingApiKey);

    public async Task SaveApiKeyAsync(string apiKey)
        => await _settings.SetAsync(SettingApiKey, apiKey);

    // ── Connection test ──────────────────────────────────────────────────────

    public async Task<bool> TestConnectionAsync(string apiKey)
    {
        try
        {
            var resp = await SendAsync(HttpMethod.Get, apiKey,
                "/crm/v3/objects/contacts?limit=1");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Read contacts ────────────────────────────────────────────────────────

    public async Task<HubSpotContactDto?> GetContactByIdAsync(string apiKey, string contactId)
    {
        var resp = await SendAsync(HttpMethod.Get, apiKey,
            $"/crm/v3/objects/contacts/{contactId}" +
            "?properties=firstname,lastname,email,phone,company,ncode,date_of_birth,fathername,mobilephone,createdate,lastmodifieddate");
        if (!resp.IsSuccessStatusCode) return null;
        var raw = await resp.Content.ReadFromJsonAsync<HubSpotRawContact>(_json);
        return raw == null ? null : MapContact(raw);
    }

    public async Task<List<HubSpotContactDto>> SearchContactsAsync(string apiKey, string query, int limit = 10)
    {
        var body = new
        {
            query,
            limit,
            properties = new[] { "firstname","lastname","email","phone","mobilephone","company","ncode" }
        };
        var content = new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json");
        var resp = await SendAsync(HttpMethod.Post, apiKey, "/crm/v3/objects/contacts/search", content);
        if (!resp.IsSuccessStatusCode) return new();
        var page = await resp.Content.ReadFromJsonAsync<HubSpotPagedResponse>(_json);
        return page?.Results?.Select(MapContact).ToList() ?? new();
    }

    public async Task<List<HubSpotContactDto>> GetContactsAsync(string apiKey, int limit = 100)
    {
        var results = new List<HubSpotContactDto>();
        string? after = null;

        do
        {
            var url = $"/crm/v3/objects/contacts?limit={Math.Min(limit, 100)}" +
                      "&properties=firstname,lastname,email,phone,company,ncode,date_of_birth,fathername,mobilephone,createdate,lastmodifieddate" +
                      (after != null ? $"&after={after}" : "");

            var resp = await SendAsync(HttpMethod.Get, apiKey, url);
            if (!resp.IsSuccessStatusCode) break;

            var body = await resp.Content.ReadFromJsonAsync<HubSpotPagedResponse>(_json);
            if (body?.Results == null) break;

            results.AddRange(body.Results.Select(MapContact));

            after = body.Paging?.Next?.After;
            if (results.Count >= limit) break;

        } while (after != null);

        return results;
    }

    // ── Create contact ───────────────────────────────────────────────────────

    public async Task<HubSpotContactDto> CreateContactAsync(string apiKey, CreateHubSpotContactDto dto)
    {
        var props   = ToCreateProperties(dto, includeCustom: true);
        var resp    = await PostContactAsync(HttpMethod.Post, apiKey, "/crm/v3/objects/contacts", props);
        if (!resp.IsSuccessStatusCode && HasMissingPropertyError(await resp.Content.ReadAsStringAsync()))
        {
            // Retry without custom properties — this portal hasn't created them yet
            props = ToCreateProperties(dto, includeCustom: false);
            resp  = await PostContactAsync(HttpMethod.Post, apiKey, "/crm/v3/objects/contacts", props);
        }
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"HubSpot {(int)resp.StatusCode}: {body}");
        }
        var raw = await resp.Content.ReadFromJsonAsync<HubSpotRawContact>(_json);
        return MapContact(raw ?? new HubSpotRawContact());
    }

    // ── Update contact ───────────────────────────────────────────────────────

    public async Task UpdateContactAsync(string apiKey, string hubspotId, CreateHubSpotContactDto dto)
    {
        var props = ToCreateProperties(dto, includeCustom: true);
        var resp  = await PostContactAsync(HttpMethod.Patch, apiKey, $"/crm/v3/objects/contacts/{hubspotId}", props);
        if (!resp.IsSuccessStatusCode && HasMissingPropertyError(await resp.Content.ReadAsStringAsync()))
        {
            props = ToCreateProperties(dto, includeCustom: false);
            resp  = await PostContactAsync(HttpMethod.Patch, apiKey, $"/crm/v3/objects/contacts/{hubspotId}", props);
        }
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"HubSpot {(int)resp.StatusCode}: {body}");
        }
    }

    // ── Delete contact ───────────────────────────────────────────────────────

    public async Task DeleteContactAsync(string apiKey, string hubspotId)
    {
        var resp = await SendAsync(HttpMethod.Delete, apiKey,
            $"/crm/v3/objects/contacts/{hubspotId}");
        resp.EnsureSuccessStatusCode();
    }

    // ── Find contact by ncode ────────────────────────────────────────────────

    public async Task<string?> FindContactByNcodeAsync(string apiKey, string ncode)
    {
        var body = new
        {
            filterGroups = new[]
            {
                new { filters = new[] { new { propertyName = "ncode", @operator = "EQ", value = ncode } } }
            },
            limit = 1,
            properties = new[] { "ncode" }
        };
        var content = new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json");
        var resp = await SendAsync(HttpMethod.Post, apiKey, "/crm/v3/objects/contacts/search", content);
        if (!resp.IsSuccessStatusCode) return null;
        var result = await resp.Content.ReadFromJsonAsync<HubSpotPagedResponse>(_json);
        return result?.Results?.FirstOrDefault()?.Id;
    }

    // ── Find contact by mobile ───────────────────────────────────────────────

    public async Task<string?> FindContactByMobileAsync(string apiKey, string mobile)
    {
        var body = new
        {
            filterGroups = new[]
            {
                new { filters = new[] { new { propertyName = "mobilephone", @operator = "EQ", value = mobile } } }
            },
            limit = 1,
            properties = new[] { "mobilephone" }
        };
        var content = new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json");
        var resp = await SendAsync(HttpMethod.Post, apiKey, "/crm/v3/objects/contacts/search", content);
        if (!resp.IsSuccessStatusCode) return null;
        var result = await resp.Content.ReadFromJsonAsync<HubSpotPagedResponse>(_json);
        return result?.Results?.FirstOrDefault()?.Id;
    }

    // ── Save contact locally (upsert by HsObjectId) ──────────────────────────

    public async Task<bool> SaveContactLocallyAsync(
        HubSpotContactDto contact,
        string?           sourceDealId = null)
    {
        if (string.IsNullOrWhiteSpace(contact.Id))
            return false;

        var existing = await _db.HubSpotContacts
            .FirstOrDefaultAsync(c => c.HsObjectId == contact.Id);

        if (existing == null)
        {
            _db.HubSpotContacts.Add(new HubSpotContact
            {
                HsObjectId   = contact.Id,
                FirstName    = contact.FirstName,
                LastName     = contact.LastName,
                Email        = contact.Email,
                Phone        = contact.Phone,
                Company      = contact.Company,
                NCode        = contact.NCode,
                BirthDate    = contact.BirthDate,
                FatherName   = contact.FatherName,
                MobilePhone  = contact.MobilePhone,
                SourceDealId = sourceDealId,
                HsCreatedAt  = contact.CreateDate,
                HsUpdatedAt  = contact.LastModifiedDate,
                CreatedAt    = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            return true;
        }

        existing.FirstName   = contact.FirstName;
        existing.LastName    = contact.LastName;
        existing.Email       = contact.Email;
        existing.Phone       = contact.Phone;
        existing.Company     = contact.Company;
        existing.NCode       = contact.NCode;
        existing.BirthDate   = contact.BirthDate;
        existing.FatherName  = contact.FatherName;
        existing.MobilePhone = contact.MobilePhone;
        existing.HsUpdatedAt = contact.LastModifiedDate ?? DateTime.UtcNow;
        existing.UpdatedAt   = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return false;
    }

    // ── Write / sync ─────────────────────────────────────────────────────────

    public async Task<HubSpotSyncResultDto> SyncContactAsync(
        string            apiKey,
        HubSpotContactDto contact)
    {
        var result = new HubSpotSyncResultDto();
        try
        {
            if (!string.IsNullOrWhiteSpace(contact.Email))
            {
                var existingId = await FindByEmailAsync(apiKey, contact.Email);
                if (existingId != null)
                {
                    var updateDto = new CreateHubSpotContactDto
                    {
                        FirstName   = contact.FirstName ?? "",
                        LastName    = contact.LastName ?? "",
                        Email       = contact.Email,
                        MobilePhone = contact.Phone,
                        Company     = contact.Company
                    };
                    await UpdateContactAsync(apiKey, existingId, updateDto);
                    result.ContactsUpdated++;
                }
                else
                {
                    var createDto = new CreateHubSpotContactDto
                    {
                        FirstName   = contact.FirstName ?? "",
                        LastName    = contact.LastName ?? "",
                        Email       = contact.Email,
                        MobilePhone = contact.Phone,
                        Company     = contact.Company
                    };
                    await CreateContactAsync(apiKey, createDto);
                    result.ContactsCreated++;
                }
            }
            else
            {
                var createDto = new CreateHubSpotContactDto
                {
                    FirstName   = contact.FirstName ?? "",
                    LastName    = contact.LastName ?? "",
                    Email       = contact.Email,
                    MobilePhone = contact.Phone,
                    Company     = contact.Company
                };
                await CreateContactAsync(apiKey, createDto);
                result.ContactsCreated++;
            }
            result.ContactsSynced = 1;
        }
        catch (Exception ex)
        {
            result.Errors++;
            result.ErrorMessages.Add(ex.Message);
        }
        return result;
    }

    public async Task<HubSpotSyncResultDto> SyncAllContactsAsync(
        string                  apiKey,
        List<HubSpotContactDto> contacts)
    {
        var total = new HubSpotSyncResultDto();
        var sem   = new SemaphoreSlim(ContactFetchLimit);
        var tasks = contacts.Select(async c =>
        {
            await sem.WaitAsync();
            try   { return await SyncContactAsync(apiKey, c); }
            finally { sem.Release(); }
        });

        var results = await Task.WhenAll(tasks);
        foreach (var r in results)
        {
            total.ContactsSynced  += r.ContactsSynced;
            total.ContactsCreated += r.ContactsCreated;
            total.ContactsUpdated += r.ContactsUpdated;
            total.Errors          += r.Errors;
            total.ErrorMessages.AddRange(r.ErrorMessages);
        }
        return total;
    }

    // ── Read deals with optional date filter ─────────────────────────────────

    public async Task<List<HubSpotDealDto>> GetDealsAsync(
        string    apiKey,
        DateTime? from  = null,
        DateTime? to    = null,
        int       limit = 200)
    {
        var results = new List<HubSpotDealDto>();
        string? after = null;

        do
        {
            var filters = new List<object>();
            if (from.HasValue)
                filters.Add(new { propertyName = "closedate", @operator = "GTE", value = from.Value.ToString("yyyy-MM-dd") });
            if (to.HasValue)
                filters.Add(new { propertyName = "closedate", @operator = "LTE", value = to.Value.ToString("yyyy-MM-dd") });

            var searchBody = new
            {
                filterGroups = filters.Count > 0
                    ? new[] { new { filters } }
                    : Array.Empty<object>(),
                properties   = new[] { "dealname","amount","deal_currency_code","dealstage","description","closedate","createdate" },
                associations = new[] { "contacts" },
                limit        = Math.Min(limit - results.Count, 100),
                after
            };

            var content = new StringContent(
                JsonSerializer.Serialize(searchBody, _json), Encoding.UTF8, "application/json");
            var resp = await SendAsync(HttpMethod.Post, apiKey,
                "/crm/v3/objects/deals/search", content);
            if (!resp.IsSuccessStatusCode) break;

            var page = await resp.Content.ReadFromJsonAsync<HubSpotDealsPagedResponse>(_json);
            if (page?.Results == null) break;

            var batch = page.Results.Select(MapDeal).ToList();

            var sem = new SemaphoreSlim(ContactFetchLimit);
            await Task.WhenAll(page.Results.Zip(batch, (raw, deal) =>
                ResolveContactAsync(apiKey, raw, deal, sem)));

            results.AddRange(batch);
            after = page.Paging?.Next?.After;
            if (results.Count >= limit) break;

        } while (after != null);

        return results;
    }

    // ── Get single deal by ID ────────────────────────────────────────────────

    public async Task<HubSpotDealDto?> GetDealByIdAsync(string apiKey, string dealId)
    {
        var resp = await SendAsync(HttpMethod.Get, apiKey,
            $"/crm/v3/objects/deals/{dealId}?properties=dealname,amount,deal_currency_code,dealstage,description,closedate,createdate&associations=contacts");
        if (!resp.IsSuccessStatusCode) return null;

        var raw  = await resp.Content.ReadFromJsonAsync<HubSpotRawDeal>(_json);
        if (raw == null) return null;

        var deal = MapDeal(raw);
        var sem  = new SemaphoreSlim(1);
        await ResolveContactAsync(apiKey, raw, deal, sem);

        // Also fetch line items
        deal.LineItems = await GetLineItemsAsync(apiKey, dealId);
        return deal;
    }

    // ── Create deal ──────────────────────────────────────────────────────────

    public async Task<HubSpotDealDto> CreateDealAsync(string apiKey, CreateHubSpotDealDto dto)
    {
        var properties = new Dictionary<string, object?>
        {
            ["dealname"]  = dto.DealName,
            ["dealstage"] = dto.Stage,
            ["description"] = dto.Description,
            ["amount"]    = dto.Amount > 0 ? (object)dto.Amount : null,
            ["closedate"] = dto.CloseDate?.ToString("yyyy-MM-dd")
        };

        object payload;
        if (!string.IsNullOrWhiteSpace(dto.AssociatedContactId))
        {
            payload = new
            {
                properties,
                associations = new[]
                {
                    new
                    {
                        to   = new { id = dto.AssociatedContactId },
                        types = new[] { new { associationCategory = "HUBSPOT_DEFINED", associationTypeId = 3 } }
                    }
                }
            };
        }
        else
        {
            payload = new { properties };
        }

        var content = new StringContent(
            JsonSerializer.Serialize(payload, _json), Encoding.UTF8, "application/json");
        var resp = await SendAsync(HttpMethod.Post, apiKey, "/crm/v3/objects/deals", content);
        resp.EnsureSuccessStatusCode();

        var raw  = await resp.Content.ReadFromJsonAsync<HubSpotRawDeal>(_json);
        return MapDeal(raw ?? new HubSpotRawDeal());
    }

    // ── Update deal ──────────────────────────────────────────────────────────

    public async Task UpdateDealAsync(string apiKey, string dealId, CreateHubSpotDealDto dto)
    {
        var properties = new Dictionary<string, object?>
        {
            ["dealname"]    = dto.DealName,
            ["dealstage"]   = dto.Stage,
            ["description"] = dto.Description,
            ["amount"]      = dto.Amount > 0 ? (object)dto.Amount : null,
            ["closedate"]   = dto.CloseDate?.ToString("yyyy-MM-dd")
        };
        var payload = new { properties };
        var content = new StringContent(
            JsonSerializer.Serialize(payload, _json), Encoding.UTF8, "application/json");
        var resp = await SendAsync(HttpMethod.Patch, apiKey,
            $"/crm/v3/objects/deals/{dealId}", content);
        resp.EnsureSuccessStatusCode();

        // Associate contact if provided (PUT replaces existing associations)
        if (!string.IsNullOrWhiteSpace(dto.AssociatedContactId))
        {
            var assocPayload = new
            {
                inputs = new[]
                {
                    new
                    {
                        from = new { id = dealId },
                        to   = new { id = dto.AssociatedContactId },
                        types = new[] { new { associationCategory = "HUBSPOT_DEFINED", associationTypeId = 3 } }
                    }
                }
            };
            var assocContent = new StringContent(
                JsonSerializer.Serialize(assocPayload, _json), Encoding.UTF8, "application/json");
            await SendAsync(HttpMethod.Post, apiKey,
                "/crm/v4/associations/deals/contacts/batch/create", assocContent);
        }
    }

    // ── Delete deal ──────────────────────────────────────────────────────────

    public async Task DeleteDealAsync(string apiKey, string dealId)
    {
        var resp = await SendAsync(HttpMethod.Delete, apiKey,
            $"/crm/v3/objects/deals/{dealId}");
        resp.EnsureSuccessStatusCode();
    }

    // ── Get deals for a contact ──────────────────────────────────────────────

    public async Task<List<HubSpotDealDto>> GetContactDealsAsync(string apiKey, string contactId)
    {
        var resp = await SendAsync(HttpMethod.Get, apiKey,
            $"/crm/v3/objects/contacts/{contactId}/associations/deals");
        if (!resp.IsSuccessStatusCode) return new();

        var assocPage = await resp.Content.ReadFromJsonAsync<HubSpotAssocPagedResponse>(_json);
        var dealIds = assocPage?.Results?.Select(r => r.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToList()
                      ?? new List<string?>();

        if (!dealIds.Any()) return new();

        var deals = new List<HubSpotDealDto>();
        foreach (var id in dealIds)
        {
            if (id == null) continue;
            var deal = await GetDealByIdAsync(apiKey, id);
            if (deal != null) deals.Add(deal);
        }
        return deals;
    }

    // ── Pipeline stages ──────────────────────────────────────────────────────

    public async Task<List<HubSpotDealStageDto>> GetDealStagesAsync(string apiKey)
    {
        // Get all pipelines, use the first (default) one
        var resp = await SendAsync(HttpMethod.Get, apiKey,
            "/crm/v3/pipelines/deals");
        if (!resp.IsSuccessStatusCode) return GetDefaultStages();

        var raw = await resp.Content.ReadFromJsonAsync<HubSpotPipelinesResponse>(_json);
        var defaultPipeline = raw?.Results?.FirstOrDefault();
        if (defaultPipeline?.Stages == null) return GetDefaultStages();

        return defaultPipeline.Stages
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new HubSpotDealStageDto
            {
                Id           = s.Id ?? "",
                Label        = s.Label ?? s.Id ?? "",
                DisplayOrder = s.DisplayOrder,
                Probability  = s.Metadata?.GetValueOrDefault("probability") is string p
                               && double.TryParse(p, out var prob) ? prob : null
            })
            .ToList();
    }

    private static List<HubSpotDealStageDto> GetDefaultStages() =>
        new()
        {
            new() { Id = "appointmentscheduled", Label = "زمان‌بندی جلسه",  DisplayOrder = 0 },
            new() { Id = "qualifiedtobuy",       Label = "واجد شرایط",      DisplayOrder = 1 },
            new() { Id = "presentationscheduled",Label = "ارائه زمان‌بندی", DisplayOrder = 2 },
            new() { Id = "decisionmakerboughtin",Label = "تأیید تصمیم‌گیر", DisplayOrder = 3 },
            new() { Id = "contractsent",         Label = "قرارداد ارسال‌شد",DisplayOrder = 4 },
            new() { Id = "closedwon",            Label = "بسته‌شده (برنده)",DisplayOrder = 5 },
            new() { Id = "closedlost",           Label = "بسته‌شده (باخته)",DisplayOrder = 6 },
        };

    // ── Notes ────────────────────────────────────────────────────────────────

    public async Task<List<HubSpotNoteDto>> GetDealNotesAsync(string apiKey, string dealId)
        => await GetObjectNotesAsync(apiKey, "deals", dealId);

    public async Task<List<HubSpotNoteDto>> GetContactNotesAsync(string apiKey, string contactId)
        => await GetObjectNotesAsync(apiKey, "contacts", contactId);

    private async Task<List<HubSpotNoteDto>> GetObjectNotesAsync(
        string apiKey, string objectType, string objectId)
    {
        // First get associations to notes
        var resp = await SendAsync(HttpMethod.Get, apiKey,
            $"/crm/v3/objects/{objectType}/{objectId}/associations/notes");
        if (!resp.IsSuccessStatusCode) return new();

        var assocPage = await resp.Content.ReadFromJsonAsync<HubSpotAssocPagedResponse>(_json);
        var noteIds   = assocPage?.Results?.Select(r => r.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToList()
                        ?? new List<string?>();
        if (!noteIds.Any()) return new();

        var notes = new List<HubSpotNoteDto>();
        foreach (var id in noteIds)
        {
            if (id == null) continue;
            var noteResp = await SendAsync(HttpMethod.Get, apiKey,
                $"/crm/v3/objects/notes/{id}?properties=hs_note_body,hs_timestamp,hubspot_owner_id");
            if (!noteResp.IsSuccessStatusCode) continue;
            var raw = await noteResp.Content.ReadFromJsonAsync<HubSpotRawNote>(_json);
            if (raw == null) continue;
            notes.Add(new HubSpotNoteDto
            {
                Id                = raw.Id ?? "",
                Body              = raw.Properties?.GetValueOrDefault("hs_note_body") ?? "",
                CreatedAt         = DateTime.TryParse(raw.Properties?.GetValueOrDefault("hs_timestamp"), out var dt) ? dt : null,
                CreatedBy         = raw.Properties?.GetValueOrDefault("hubspot_owner_id"),
                AssociatedObjectId = objectId
            });
        }
        return notes.OrderByDescending(n => n.CreatedAt).ToList();
    }

    public async Task<HubSpotNoteDto> AddNoteAsync(
        string  apiKey,
        string  body,
        string? dealId,
        string? contactId)
    {
        var associations = new List<object>();

        if (!string.IsNullOrWhiteSpace(dealId))
            associations.Add(new
            {
                to    = new { id = dealId },
                types = new[] { new { associationCategory = "HUBSPOT_DEFINED", associationTypeId = 214 } }
            });

        if (!string.IsNullOrWhiteSpace(contactId))
            associations.Add(new
            {
                to    = new { id = contactId },
                types = new[] { new { associationCategory = "HUBSPOT_DEFINED", associationTypeId = 202 } }
            });

        var payload = new
        {
            properties = new Dictionary<string, object>
            {
                ["hs_note_body"] = body,
                ["hs_timestamp"] = DateTime.UtcNow.ToString("o")
            },
            associations
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload, _json), Encoding.UTF8, "application/json");
        var resp = await SendAsync(HttpMethod.Post, apiKey, "/crm/v3/objects/notes", content);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<HubSpotRawNote>(_json);
        return new HubSpotNoteDto
        {
            Id        = raw?.Id ?? "",
            Body      = body,
            CreatedAt = DateTime.UtcNow
        };
    }

    // ── Line items ───────────────────────────────────────────────────────────

    public async Task<string> CreateLineItemAsync(string apiKey, string dealId, HubSpotLineItemDto item)
    {
        var payload = new
        {
            properties = new Dictionary<string, object?>
            {
                ["name"]     = item.Name,
                ["quantity"] = item.Quantity,
                ["price"]    = item.UnitPrice,
                ["hs_sku"]   = item.Sku,
                ["description"] = item.Description
            },
            associations = new[]
            {
                new
                {
                    to    = new { id = dealId },
                    types = new[] { new { associationCategory = "HUBSPOT_DEFINED", associationTypeId = 20 } }
                }
            }
        };
        var content = new StringContent(JsonSerializer.Serialize(payload, _json), Encoding.UTF8, "application/json");
        var resp = await SendAsync(HttpMethod.Post, apiKey, "/crm/v3/objects/line_items", content);
        resp.EnsureSuccessStatusCode();
        var raw = await resp.Content.ReadFromJsonAsync<HubSpotRawContact>(_json);
        return raw?.Id ?? "";
    }

    public async Task DeleteLineItemAsync(string apiKey, string lineItemId)
    {
        var resp = await SendAsync(HttpMethod.Delete, apiKey, $"/crm/v3/objects/line_items/{lineItemId}");
        resp.EnsureSuccessStatusCode();
    }

    // ── Products ─────────────────────────────────────────────────────────────

    public async Task<List<HubSpotProductDto>> GetProductsAsync(string apiKey)
    {
        var results = new List<HubSpotProductDto>();
        string? after = null;
        do
        {
            var url = "/crm/v3/objects/products?limit=100&properties=name,price,hs_sku,description"
                      + (after != null ? $"&after={after}" : "");
            var resp = await SendAsync(HttpMethod.Get, apiKey, url);
            if (!resp.IsSuccessStatusCode) break;
            var page = await resp.Content.ReadFromJsonAsync<HubSpotPagedResponse>(_json);
            if (page?.Results == null) break;
            foreach (var raw in page.Results)
            {
                var p = raw.Properties ?? new Dictionary<string, string>();
                decimal.TryParse(p.GetValueOrDefault("price"), out var price);
                results.Add(new HubSpotProductDto
                {
                    Id          = raw.Id ?? "",
                    Name        = p.GetValueOrDefault("name"),
                    Price       = price,
                    Sku         = p.GetValueOrDefault("hs_sku"),
                    Description = p.GetValueOrDefault("description")
                });
            }
            after = page.Paging?.Next?.After;
        } while (after != null);
        return results;
    }

    // ── Calls ─────────────────────────────────────────────────────────────────

    public Task<List<HubSpotCallDto>> GetContactCallsAsync(string apiKey, string contactId)
        => GetObjectCallsAsync(apiKey, "contacts", contactId);

    public Task<List<HubSpotCallDto>> GetDealCallsAsync(string apiKey, string dealId)
        => GetObjectCallsAsync(apiKey, "deals", dealId);

    private async Task<List<HubSpotCallDto>> GetObjectCallsAsync(
        string apiKey, string objectType, string objectId)
    {
        var resp = await SendAsync(HttpMethod.Get, apiKey,
            $"/crm/v3/objects/{objectType}/{objectId}/associations/calls");
        if (!resp.IsSuccessStatusCode) return new();

        var assocPage = await resp.Content.ReadFromJsonAsync<HubSpotAssocPagedResponse>(_json);
        var callIds = assocPage?.Results?
            .Select(r => r.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList() ?? new List<string?>();
        if (!callIds.Any()) return new();

        var calls = new List<HubSpotCallDto>();
        foreach (var id in callIds)
        {
            if (id == null) continue;
            var callResp = await SendAsync(HttpMethod.Get, apiKey,
                $"/crm/v3/objects/calls/{id}?properties=hs_call_title,hs_call_body,hs_call_status,hs_call_direction,hs_call_duration,hs_timestamp");
            if (!callResp.IsSuccessStatusCode) continue;
            var raw = await callResp.Content.ReadFromJsonAsync<HubSpotRawNote>(_json);
            if (raw == null) continue;
            var p = raw.Properties ?? new Dictionary<string, string>();
            int.TryParse(p.GetValueOrDefault("hs_call_duration"), out var durMs);
            calls.Add(new HubSpotCallDto
            {
                Id              = raw.Id ?? "",
                Title           = p.GetValueOrDefault("hs_call_title"),
                Body            = p.GetValueOrDefault("hs_call_body"),
                Status          = p.GetValueOrDefault("hs_call_status"),
                Direction       = p.GetValueOrDefault("hs_call_direction"),
                DurationSeconds = durMs > 0 ? durMs / 1000 : null,
                CreatedAt       = DateTime.TryParse(p.GetValueOrDefault("hs_timestamp"), out var dt) ? dt : null,
                AssociatedObjectId = objectId
            });
        }
        return calls.OrderByDescending(c => c.CreatedAt).ToList();
    }

    // ── Files ─────────────────────────────────────────────────────────────────

    public async Task<HubSpotFileDto> UploadFileAsync(
        string apiKey, string fileName, byte[] data, string mimeType)
    {
        // HubSpot Files API v3: "options" must be a JSON string, "folderPath" = "/",
        // and MultipartFormDataContent.Add(content, name) name must NOT be empty string.
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("{\"access\":\"PUBLIC_INDEXABLE\"}"), "options");
        form.Add(new StringContent("/"),                                 "folderPath");
        var fileContent = new ByteArrayContent(data);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
        form.Add(fileContent, "file", fileName);

        // The Files API uses a different base URL but same auth
        var client  = _httpClientFactory.CreateClient("hubspot");
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.hubapi.com/files/v3/files");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = form;

        var resp = await client.SendAsync(request);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"HubSpot Files {(int)resp.StatusCode}: {body}");
        }

        var raw = await resp.Content.ReadFromJsonAsync<HubSpotRawFile>(_json);
        return new HubSpotFileDto
        {
            Id        = raw?.Id ?? "",
            Name      = raw?.Name ?? fileName,
            Url       = raw?.Url,
            Extension = raw?.Extension,
            Size      = raw?.Size,
            CreatedAt = DateTime.TryParse(raw?.CreatedAt, out var dt) ? dt : null
        };
    }

    public async Task<List<HubSpotFileDto>> GetFilesForObjectAsync(
        string apiKey, string objectType, string objectId)
    {
        // Files are stored as note attachments: fetch notes and extract file metadata from body
        var notes = await GetObjectNotesAsync(apiKey, objectType, objectId);
        var files = new List<HubSpotFileDto>();
        foreach (var note in notes)
        {
            if (string.IsNullOrWhiteSpace(note.Body)) continue;
            // Notes created by file upload have body format: "[FILE] name|url"
            if (note.Body.StartsWith("[FILE] "))
            {
                var parts = note.Body.Substring(7).Split('|');
                files.Add(new HubSpotFileDto
                {
                    Id        = note.Id,
                    Name      = parts.Length > 0 ? parts[0] : "file",
                    Url       = parts.Length > 1 ? parts[1] : null,
                    CreatedAt = note.CreatedAt
                });
            }
        }
        return files;
    }

    public async Task<List<HubSpotLineItemDto>> GetLineItemsAsync(string apiKey, string dealId)
    {
        var resp = await SendAsync(HttpMethod.Get, apiKey,
            $"/crm/v3/objects/deals/{dealId}/associations/line_items");
        if (!resp.IsSuccessStatusCode) return new();

        var assocPage = await resp.Content.ReadFromJsonAsync<HubSpotAssocPagedResponse>(_json);
        var lineItemIds = assocPage?.Results?.Select(r => r.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToList()
                          ?? new List<string?>();
        if (!lineItemIds.Any()) return new();

        var items = new List<HubSpotLineItemDto>();
        foreach (var id in lineItemIds)
        {
            if (id == null) continue;
            var itemResp = await SendAsync(HttpMethod.Get, apiKey,
                $"/crm/v3/objects/line_items/{id}?properties=name,quantity,price,amount,hs_sku,description");
            if (!itemResp.IsSuccessStatusCode) continue;
            var raw = await itemResp.Content.ReadFromJsonAsync<HubSpotRawContact>(_json);
            if (raw?.Properties == null) continue;
            var p = raw.Properties;
            items.Add(new HubSpotLineItemDto
            {
                Id          = raw.Id ?? "",
                Name        = p.GetValueOrDefault("name"),
                Quantity    = decimal.TryParse(p.GetValueOrDefault("quantity"),  out var q) ? q : 1,
                UnitPrice   = decimal.TryParse(p.GetValueOrDefault("price"),     out var u) ? u : 0,
                Amount      = decimal.TryParse(p.GetValueOrDefault("amount"),    out var a) ? a : 0,
                Sku         = p.GetValueOrDefault("hs_sku"),
                Description = p.GetValueOrDefault("description")
            });
        }
        return items;
    }

    // ── Private resolve contact for a deal ──────────────────────────────────

    private async Task ResolveContactAsync(
        string         apiKey,
        HubSpotRawDeal raw,
        HubSpotDealDto deal,
        SemaphoreSlim  sem)
    {
        var contactId = raw.Associations?
            .GetValueOrDefault("contacts")?.Results?.FirstOrDefault()?.Id;

        if (string.IsNullOrWhiteSpace(contactId)) return;

        deal.AssociatedContactId = contactId;

        await sem.WaitAsync();
        try
        {
            var props = await FetchContactPropsAsync(apiKey, contactId);
            if (props != null)
            {
                deal.ContactFirstName = props.GetValueOrDefault("firstname");
                deal.ContactLastName  = props.GetValueOrDefault("lastname");
                deal.ContactEmail     = props.GetValueOrDefault("email");
                deal.ContactPhone     = props.GetValueOrDefault("phone");
                deal.ContactCompany   = props.GetValueOrDefault("company");
            }
        }
        finally { sem.Release(); }
    }

    // ── Private HTTP helpers ─────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod   method,
        string       apiKey,
        string       path,
        HttpContent? content = null)
    {
        var client  = _httpClientFactory.CreateClient("hubspot");
        var request = new HttpRequestMessage(method, BaseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        if (content != null) request.Content = content;
        return await client.SendAsync(request);
    }

    private async Task<string?> FindByEmailAsync(string apiKey, string email)
    {
        var body = new
        {
            filterGroups = new[]
            {
                new { filters = new[] { new { propertyName = "email", @operator = "EQ", value = email } } }
            },
            limit = 1,
            properties = new[] { "email" }
        };
        var content = new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json");
        var resp = await SendAsync(HttpMethod.Post, apiKey,
            "/crm/v3/objects/contacts/search", content);
        if (!resp.IsSuccessStatusCode) return null;
        var result = await resp.Content.ReadFromJsonAsync<HubSpotPagedResponse>(_json);
        return result?.Results?.FirstOrDefault()?.Id;
    }

    private async Task<Dictionary<string, string>?> FetchContactPropsAsync(
        string apiKey, string contactId)
    {
        var resp = await SendAsync(HttpMethod.Get, apiKey,
            $"/crm/v3/objects/contacts/{contactId}" +
            "?properties=firstname,lastname,email,phone,company,ncode,date_of_birth,fathername,mobilephone");
        if (!resp.IsSuccessStatusCode) return null;
        var raw = await resp.Content.ReadFromJsonAsync<HubSpotRawContact>(_json);
        return raw?.Properties;
    }

    // ── Mappers ──────────────────────────────────────────────────────────────

    private static HubSpotContactDto MapContact(HubSpotRawContact raw) => new()
    {
        Id               = raw.Id ?? string.Empty,
        FirstName        = raw.Properties?.GetValueOrDefault("firstname"),
        LastName         = raw.Properties?.GetValueOrDefault("lastname"),
        Email            = raw.Properties?.GetValueOrDefault("email"),
        Phone            = raw.Properties?.GetValueOrDefault("phone"),
        Company          = raw.Properties?.GetValueOrDefault("company"),
        NCode            = raw.Properties?.GetValueOrDefault("ncode"),
        BirthDate        = raw.Properties?.GetValueOrDefault("date_of_birth"),
        FatherName       = raw.Properties?.GetValueOrDefault("fathername"),
        MobilePhone      = raw.Properties?.GetValueOrDefault("mobilephone"),
        CreateDate       = raw.Properties != null &&
                           raw.Properties.TryGetValue("createdate", out var cd) &&
                           DateTime.TryParse(cd, out var cdt) ? cdt : null,
        LastModifiedDate = raw.Properties != null &&
                           raw.Properties.TryGetValue("lastmodifieddate", out var lm) &&
                           DateTime.TryParse(lm, out var lmt) ? lmt : null
    };

    private static HubSpotDealDto MapDeal(HubSpotRawDeal raw)
    {
        var p = raw.Properties ?? new Dictionary<string, string>();
        decimal.TryParse(p.GetValueOrDefault("amount") ?? "0", out var amount);
        return new HubSpotDealDto
        {
            Id          = raw.Id ?? string.Empty,
            DealName    = p.GetValueOrDefault("dealname") ?? $"Deal {raw.Id}",
            Amount      = amount,
            Currency    = p.GetValueOrDefault("deal_currency_code"),
            Stage       = p.GetValueOrDefault("dealstage"),
            Description = p.GetValueOrDefault("description"),
            CloseDate   = DateTime.TryParse(p.GetValueOrDefault("closedate"),  out var cd) ? cd : null,
            CreateDate  = DateTime.TryParse(p.GetValueOrDefault("createdate"), out var cr) ? cr : null,
        };
    }

    /// <summary>
    /// Returns true when a HubSpot 400 body contains a PROPERTY_DOESNT_EXIST error,
    /// meaning the portal hasn't created that custom property yet.
    /// </summary>
    private static bool HasMissingPropertyError(string responseBody) =>
        responseBody.Contains("PROPERTY_DOESNT_EXIST", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Sends a create/update request with the given properties dictionary.
    /// </summary>
    private async Task<HttpResponseMessage> PostContactAsync(
        HttpMethod method, string apiKey, string path, Dictionary<string, string> props)
    {
        var payload = new { properties = props };
        var content = new StringContent(
            JsonSerializer.Serialize(payload, _json), Encoding.UTF8, "application/json");
        return await SendAsync(method, apiKey, path, content);
    }

    /// <summary>
    /// Builds the properties dictionary for contact create / update.
    /// Rules:
    ///   • Only include a key when the value is non-null/non-empty — HubSpot rejects null values.
    ///   • Built-in properties use their official API names (firstname, lastname, email,
    ///     mobilephone, company).
    ///   • When includeCustom=true, custom properties (ncode, fathername, date_of_birth) are
    ///     appended. If the portal doesn't have those properties the caller retries with
    ///     includeCustom=false so the contact is still created without them.
    /// </summary>
    private static Dictionary<string, string> ToCreateProperties(CreateHubSpotContactDto c, bool includeCustom = true)
    {
        var props = new Dictionary<string, string>();

        // ── Standard built-in properties ────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(c.FirstName))   props["firstname"]   = c.FirstName;
        if (!string.IsNullOrWhiteSpace(c.LastName))    props["lastname"]    = c.LastName;
        if (!string.IsNullOrWhiteSpace(c.Email))       props["email"]       = c.Email;
        if (!string.IsNullOrWhiteSpace(c.MobilePhone)) props["mobilephone"] = c.MobilePhone;
        if (!string.IsNullOrWhiteSpace(c.Company))     props["company"]     = c.Company;

        // ── Custom properties — only sent when populated and allowed ─────────
        if (includeCustom)
        {
            if (!string.IsNullOrWhiteSpace(c.NCode))      props["ncode"]         = c.NCode;
            if (!string.IsNullOrWhiteSpace(c.BirthDate))  props["date_of_birth"] = c.BirthDate;
            if (!string.IsNullOrWhiteSpace(c.FatherName)) props["fathername"]    = c.FatherName;
        }

        return props;
    }

    // ── Private response models ───────────────────────────────────────────────

    private sealed class HubSpotPagedResponse
    {
        [JsonPropertyName("results")]
        public List<HubSpotRawContact>? Results { get; set; }

        [JsonPropertyName("paging")]
        public HubSpotPaging? Paging { get; set; }
    }

    private sealed class HubSpotRawContact
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("properties")]
        public Dictionary<string, string>? Properties { get; set; }
    }

    private sealed class HubSpotDealsPagedResponse
    {
        [JsonPropertyName("results")]
        public List<HubSpotRawDeal>? Results { get; set; }

        [JsonPropertyName("paging")]
        public HubSpotPaging? Paging { get; set; }
    }

    private sealed class HubSpotRawDeal
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("properties")]
        public Dictionary<string, string>? Properties { get; set; }

        [JsonPropertyName("associations")]
        public Dictionary<string, HubSpotAssociationList>? Associations { get; set; }
    }

    private sealed class HubSpotAssociationList
    {
        [JsonPropertyName("results")]
        public List<HubSpotAssociationRef>? Results { get; set; }
    }

    private sealed class HubSpotAssociationRef
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    private sealed class HubSpotAssocPagedResponse
    {
        [JsonPropertyName("results")]
        public List<HubSpotAssociationRef>? Results { get; set; }
    }

    private sealed class HubSpotPipelinesResponse
    {
        [JsonPropertyName("results")]
        public List<HubSpotRawPipeline>? Results { get; set; }
    }

    private sealed class HubSpotRawPipeline
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("stages")]
        public List<HubSpotRawStage>? Stages { get; set; }
    }

    private sealed class HubSpotRawStage
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("displayOrder")]
        public int DisplayOrder { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, string>? Metadata { get; set; }
    }

    private sealed class HubSpotRawNote
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("properties")]
        public Dictionary<string, string>? Properties { get; set; }
    }

    private sealed class HubSpotRawFile
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("extension")]
        public string? Extension { get; set; }

        [JsonPropertyName("size")]
        public long? Size { get; set; }

        [JsonPropertyName("createdAt")]
        public string? CreatedAt { get; set; }
    }

    private sealed class HubSpotPaging
    {
        [JsonPropertyName("next")]
        public HubSpotPagingNext? Next { get; set; }
    }

    private sealed class HubSpotPagingNext
    {
        [JsonPropertyName("after")]
        public string? After { get; set; }
    }

}

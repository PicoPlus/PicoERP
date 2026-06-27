using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PicoERP.Application.Common;
using PicoERP.Application.DTOs;
using PicoERP.Application.Interfaces;
using PicoERP.Infrastructure.Persistence;

namespace PicoERP.Infrastructure.Services;

/// <summary>
/// Sends SMS messages and manages patterns through the IPPanel Edge API.
/// Docs: https://docs.ippanel.com
///
/// Endpoint : POST https://edge.ippanel.com/v1/api/send
/// Auth     : Authorization: {apiKey}   (no "Bearer" prefix)
/// </summary>
public class SmsService : ISmsService
{
    private const string BaseUrl           = "https://edge.ippanel.com/v1/api";
    private const string SendUrl           = $"{BaseUrl}/send";
    private const string NumbersUrl        = $"{BaseUrl}/number/numbers";
    private const string PatternsUrl       = $"{BaseUrl}/patterns";
    private const string OutboxReportUrl   = $"{BaseUrl}/report/new_list";
    private const string SettingApiKey     = "Sms:ApiKey";
    private const string SettingSender     = "Sms:Sender";
    private const string SettingAdminPhone = "Sms:AdminPhone";

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public SmsService(AppDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public async Task<Result> SendAsync(string toPhone, string message)
    {
        var apiKey = await GetSettingAsync(SettingApiKey);
        if (string.IsNullOrWhiteSpace(apiKey))
            return Result.Failure("کلید API پیامک تنظیم نشده است.");

        var sender = await GetSettingAsync(SettingSender) ?? "+98200010000";
        return await CallIpPanelAsync(apiKey, sender, toPhone, message);
    }

    public async Task<Result> SendToAdminAsync(string message)
    {
        var adminPhone = await GetSettingAsync(SettingAdminPhone);
        if (string.IsNullOrWhiteSpace(adminPhone))
            return Result.Failure("شماره موبایل مدیر در تنظیمات پیامک وارد نشده است.");

        return await SendAsync(adminPhone, message);
    }

    public async Task<Result> TestAsync(string apiKey, string sender, string toPhone)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return Result.Failure("کلید API خالی است.");
        if (string.IsNullOrWhiteSpace(sender))
            return Result.Failure("شماره فرستنده انتخاب نشده است.");
        if (string.IsNullOrWhiteSpace(toPhone))
            return Result.Failure("شماره تلفن مدیر وارد نشده است.");

        return await CallIpPanelAsync(apiKey, sender, toPhone, "پیکو ERP: تست ارسال پیامک موفق بود ✓");
    }

    // ── IPPanel: list numbers ──────────────────────────────────────────────

    public async Task<IReadOnlyList<string>> GetNumbersAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return [];

        try
        {
            var client = CreateClient(apiKey);
            var resp = await client.GetAsync($"{NumbersUrl}?page=1&per_page=100");
            if (!resp.IsSuccessStatusCode) return [];

            var body = await resp.Content.ReadFromJsonAsync<IpPanelNumbersResponse>(_json);
            if (body?.Meta?.Status != true || body.Data is null) return [];

            return body.Data
                .Where(n => !string.IsNullOrWhiteSpace(n.Number))
                .Select(n => n.Number!)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    // ── Pattern: list ──────────────────────────────────────────────────────

    public async Task<List<IpPanelPatternDto>> GetPatternsAsync(string apiKey, int page = 1, int perPage = 50)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return [];
        try
        {
            var client = CreateClient(apiKey);
            var resp = await client.GetAsync($"{PatternsUrl}?page={page}&per_page={perPage}");
            if (!resp.IsSuccessStatusCode) return [];

            var body = await resp.Content.ReadFromJsonAsync<IpPanelPatternsListResponse>(_json);
            if (body?.Meta?.Status != true || body.Data is null) return [];

            return body.Data.Select(MapPattern).ToList();
        }
        catch { return []; }
    }

    // ── Pattern: get by code ──────────────────────────────────────────────

    public async Task<IpPanelPatternDto?> GetPatternByCodeAsync(string apiKey, string patternCode)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return null;
        try
        {
            var client = CreateClient(apiKey);
            var resp = await client.GetAsync($"{PatternsUrl}/{patternCode}");
            if (!resp.IsSuccessStatusCode) return null;

            var body = await resp.Content.ReadFromJsonAsync<IpPanelPatternSingleResponse>(_json);
            return body?.Data is null ? null : MapPattern(body.Data);
        }
        catch { return null; }
    }

    // ── Pattern: create ────────────────────────────────────────────────────

    public async Task<Result<IpPanelPatternDto>> CreatePatternAsync(string apiKey, CreatePatternDto dto)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return Result<IpPanelPatternDto>.Failure("کلید API تنظیم نشده است.");
        try
        {
            var client = CreateClient(apiKey);
            var payload = new
            {
                title       = dto.Title,
                description = dto.Description,
                is_share    = dto.IsShare,
                message     = dto.Message,
                website     = dto.Website,
                variable    = dto.Variable.Select(v => new { name = v.Name, type = v.Type }).ToArray()
            };
            var content = new StringContent(JsonSerializer.Serialize(payload, _json), Encoding.UTF8, "application/json");
            var resp = await client.PostAsync($"{BaseUrl}/user/pattern", content);
            var body = await resp.Content.ReadFromJsonAsync<IpPanelPatternSingleResponse>(_json);

            if (body?.Meta?.Status == true && body.Data != null)
                return Result<IpPanelPatternDto>.Success(MapPattern(body.Data));

            var err = BuildError(body?.Meta, resp);
            return Result<IpPanelPatternDto>.Failure(err);
        }
        catch (Exception ex)
        {
            return Result<IpPanelPatternDto>.Failure($"خطا: {ex.Message}");
        }
    }

    // ── Pattern: update ────────────────────────────────────────────────────

    public async Task<Result<IpPanelPatternDto>> UpdatePatternAsync(string apiKey, string patternCode, CreatePatternDto dto)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return Result<IpPanelPatternDto>.Failure("کلید API تنظیم نشده است.");
        try
        {
            var client = CreateClient(apiKey);
            var payload = new
            {
                title       = dto.Title,
                description = dto.Description,
                is_share    = dto.IsShare,
                message     = dto.Message,
                website     = dto.Website,
                variable    = dto.Variable.Select(v => new { name = v.Name, type = v.Type }).ToArray()
            };
            var content = new StringContent(JsonSerializer.Serialize(payload, _json), Encoding.UTF8, "application/json");
            var req = new HttpRequestMessage(HttpMethod.Put, $"{PatternsUrl}/normal/{patternCode}") { Content = content };
            var resp = await client.SendAsync(req);
            var body = await resp.Content.ReadFromJsonAsync<IpPanelPatternSingleResponse>(_json);

            if (body?.Meta?.Status == true && body.Data != null)
                return Result<IpPanelPatternDto>.Success(MapPattern(body.Data));

            var err = BuildError(body?.Meta, resp);
            return Result<IpPanelPatternDto>.Failure(err);
        }
        catch (Exception ex)
        {
            return Result<IpPanelPatternDto>.Failure($"خطا: {ex.Message}");
        }
    }

    // ── Pattern: delete ────────────────────────────────────────────────────

    public async Task<Result> DeletePatternAsync(string apiKey, string patternCode)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return Result.Failure("کلید API تنظیم نشده است.");
        try
        {
            var client = CreateClient(apiKey);
            var req = new HttpRequestMessage(HttpMethod.Delete, $"{PatternsUrl}/normal/{patternCode}");
            var resp = await client.SendAsync(req);
            var body = await resp.Content.ReadFromJsonAsync<IpPanelBaseResponse>(_json);

            if (body?.Meta?.Status == true) return Result.Success();
            return Result.Failure(BuildError(body?.Meta, resp));
        }
        catch (Exception ex)
        {
            return Result.Failure($"خطا: {ex.Message}");
        }
    }

    // ── Pattern SMS send ───────────────────────────────────────────────────

    public async Task<Result<string>> SendPatternAsync(string apiKey, string patternCode, string toPhone,
        Dictionary<string, string> variables, string? fromNumber = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return Result<string>.Failure("کلید API تنظیم نشده است.");

        if (string.IsNullOrWhiteSpace(fromNumber))
            fromNumber = await GetSettingAsync(SettingSender) ?? "";

        try
        {
            var client = CreateClient(apiKey);
            // Correct endpoint per IPPanel Edge docs:
            //   POST /v1/api/send  with sending_type="pattern"
            //   Fields: code (not pattern_code), recipients[] (not to), params{} (not variable)
            //
            // from_number: use exactly as stored (e.g. +983000505) — do NOT re-normalize,
            //              the sender number must match what is registered in IPPanel exactly.
            // recipients : must be E.164 format (+989xxxxxxxxx)
            var payload = new
            {
                sending_type = "pattern",
                from_number  = fromNumber.Trim(),
                code         = patternCode,
                recipients   = new[] { NormalizeNumberE164(toPhone) },
                @params      = variables
            };
            var content = new StringContent(JsonSerializer.Serialize(payload, _json), Encoding.UTF8, "application/json");
            var resp = await client.PostAsync(SendUrl, content);
            var body = await resp.Content.ReadFromJsonAsync<IpPanelSendPatternResponse>(_json);

            if (body?.Meta?.Status == true)
                return Result<string>.Success(
                    body.Data?.MessageOutboxIds?.FirstOrDefault().ToString()
                    ?? body.Data?.MessageId
                    ?? body.Data?.Id
                    ?? "");

            return Result<string>.Failure(BuildError(body?.Meta, resp));
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"خطا در ارسال پترن: {ex.Message}");
        }
    }

    // ── Outbox report ──────────────────────────────────────────────────────

    public async Task<List<IpPanelOutboxReportDto>> GetOutboxReportAsync(string apiKey, int page = 1, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return [];
        try
        {
            var client = CreateClient(apiKey);
            var payload = new { page, limit, filters = new { username = "*" } };
            var content = new StringContent(JsonSerializer.Serialize(payload, _json), Encoding.UTF8, "application/json");
            var resp = await client.PostAsync(OutboxReportUrl, content);
            if (!resp.IsSuccessStatusCode) return [];

            var body = await resp.Content.ReadFromJsonAsync<IpPanelOutboxReportResponse>(_json);
            if (body?.Meta?.Status != true || body.Data is null) return [];

            return body.Data.Select(d => new IpPanelOutboxReportDto
            {
                MessagesOutboxId = d.MessagesOutboxId,
                Number           = d.Number,
                Message          = d.Message,
                Status           = d.Status,
                Type             = d.Type,
                TimeSend         = d.TimeSend,
                RcptsCount       = d.RcptsCount,
                ExitCount        = d.ExitCount,
                Cost             = d.Cost,
                StateId          = d.StateId
            }).ToList();
        }
        catch { return []; }
    }

    // ── IPPanel REST call (webservice SMS) ─────────────────────────────────

    private async Task<Result> CallIpPanelAsync(string apiKey, string sender, string receptor, string message)
    {
        try
        {
            var client = CreateClient(apiKey);

            var payload = new IpPanelRequest
            {
                SendingType = "webservice",
                FromNumber  = sender.Trim(),          // use exactly as stored in settings
                Message     = message,
                Params      = new IpPanelParams { Recipients = [NormalizeNumberE164(receptor)] }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload, _json), Encoding.UTF8, "application/json");
            var resp = await client.PostAsync(SendUrl, content);
            var body = await resp.Content.ReadFromJsonAsync<IpPanelResponse>(_json);

            if (body?.Meta?.Status == true)
                return Result.Success();

            var errMsg = BuildError(body?.Meta, resp);
            return Result.Failure($"خطای IPPanel: {errMsg}");
        }
        catch (Exception ex)
        {
            return Result.Failure($"خطا در ارسال پیامک: {ex.Message}");
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private HttpClient CreateClient(string apiKey)
    {
        var client = _httpClientFactory.CreateClient("ippanel");
        client.DefaultRequestHeaders.Remove("Authorization");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", apiKey);
        return client;
    }

    private static string BuildError(IpPanelMeta? meta, HttpResponseMessage resp)
        => meta?.Message
           ?? (meta?.Errors != null
               ? string.Join(", ", meta.Errors.SelectMany(e => e.Value))
               : $"HTTP {(int)resp.StatusCode}");

    private static string NormalizeNumber(string number)
    {
        var n = number.TrimStart('+').Trim();
        if (n.StartsWith('0'))
            n = "98" + n[1..];
        return n;
    }

    // IPPanel pattern endpoint requires E.164 format: +989120000000
    private static string NormalizeNumberE164(string number)
    {
        var n = NormalizeNumber(number); // strips + and converts 0xxx -> 98xxx
        return "+" + n;
    }

    private async Task<string?> GetSettingAsync(string key)
        => (await _db.AppSettings.AsNoTracking()
               .FirstOrDefaultAsync(s => s.Key == key))?.Value;

    private static IpPanelPatternDto MapPattern(IpPanelPatternItem p) => new()
    {
        Id                = p.Id ?? string.Empty,
        PatternCode       = p.PatternCode ?? string.Empty,
        Title             = p.Title,
        PatternMessage    = p.PatternMessage ?? string.Empty,
        PatternDescription = p.PatternDescription,
        PatternStatus     = p.PatternStatus ?? string.Empty,
        PatternStatusFa   = p.PatternStatusFa,
        PatternIsShare    = p.PatternIsShare,
        PatternType       = p.PatternType ?? "normal",
        Delimiter         = p.Delimiter ?? "%",
        Variable          = p.Variable?.Select(v => new IpPanelPatternVariable
        {
            Name = v.Name ?? string.Empty,
            Type = v.Type ?? "string",
            Len  = v.Len
        }).ToList() ?? [],
        UpdatedAt = p.UpdatedAt
    };

    // ── Request / Response models ─────────────────────────────────────────

    private sealed class IpPanelRequest
    {
        public string SendingType { get; set; } = "webservice";
        public string FromNumber  { get; set; } = string.Empty;
        public string Message     { get; set; } = string.Empty;
        public IpPanelParams Params { get; set; } = new();
    }

    private sealed class IpPanelParams
    {
        public string[] Recipients { get; set; } = [];
    }

    private sealed class IpPanelResponse
    {
        public IpPanelMeta? Meta { get; set; }
    }

    private sealed class IpPanelBaseResponse
    {
        public IpPanelMeta? Meta { get; set; }
    }

    private sealed class IpPanelNumbersResponse
    {
        public List<IpPanelNumberItem>? Data { get; set; }
        public IpPanelMeta? Meta { get; set; }
    }

    private sealed class IpPanelNumberItem
    {
        public int?    Id         { get; set; }
        public string? Number     { get; set; }

        [JsonPropertyName("operator_id")]
        public int?    OperatorId { get; set; }

        public string? Alias { get; set; }
    }

    private sealed class IpPanelPatternsListResponse
    {
        public List<IpPanelPatternItem>? Data { get; set; }
        public IpPanelPaginatedMeta? Meta { get; set; }
    }

    private sealed class IpPanelPatternSingleResponse
    {
        public IpPanelPatternItem? Data { get; set; }
        public IpPanelMeta? Meta { get; set; }
    }

    private sealed class IpPanelPatternItem
    {
        public string? Id { get; set; }

        [JsonPropertyName("pattern_code")]
        public string? PatternCode { get; set; }

        public string? Title { get; set; }

        [JsonPropertyName("pattern_message")]
        public string? PatternMessage { get; set; }

        [JsonPropertyName("pattern_description")]
        public string? PatternDescription { get; set; }

        [JsonPropertyName("pattern_status")]
        public string? PatternStatus { get; set; }

        [JsonPropertyName("pattern_status_fa")]
        public string? PatternStatusFa { get; set; }

        [JsonPropertyName("pattern_is_share")]
        public bool PatternIsShare { get; set; }

        [JsonPropertyName("pattern_type")]
        public string? PatternType { get; set; }

        public string? Delimiter { get; set; }

        public List<IpPanelVarItem>? Variable { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }

    private sealed class IpPanelVarItem
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public int Len { get; set; }
    }

    private sealed class IpPanelSendPatternResponse
    {
        public IpPanelSendPatternData? Data { get; set; }
        public IpPanelMeta? Meta { get; set; }
    }

    private sealed class IpPanelSendPatternData
    {
        public string? Id { get; set; }

        [JsonPropertyName("message_id")]
        public string? MessageId { get; set; }

        [JsonPropertyName("message_outbox_ids")]
        public List<long>? MessageOutboxIds { get; set; }
    }

    private sealed class IpPanelOutboxReportResponse
    {
        public List<IpPanelOutboxItem>? Data { get; set; }
        public IpPanelPaginatedMeta? Meta { get; set; }
    }

    private sealed class IpPanelOutboxItem
    {
        [JsonPropertyName("messages_outbox_id")]
        public string? MessagesOutboxId { get; set; }

        public string? Number { get; set; }
        public string? Message { get; set; }
        public string? Status { get; set; }
        public string? Type { get; set; }

        [JsonPropertyName("time_send")]
        public string? TimeSend { get; set; }

        [JsonPropertyName("rcpts_count")]
        public string? RcptsCount { get; set; }

        [JsonPropertyName("exit_count")]
        public string? ExitCount { get; set; }

        public decimal? Cost { get; set; }

        [JsonPropertyName("state_id")]
        public int? StateId { get; set; }
    }

    private sealed class IpPanelMeta
    {
        public bool    Status  { get; set; }
        public string? Message { get; set; }

        [JsonPropertyName("message_code")]
        public string? MessageCode { get; set; }

        public Dictionary<string, List<string>>? Errors { get; set; }
    }

    private sealed class IpPanelPaginatedMeta
    {
        public bool    Status  { get; set; }
        public string? Message { get; set; }

        [JsonPropertyName("current_page")]
        public int CurrentPage { get; set; }

        [JsonPropertyName("last_page")]
        public int LastPage { get; set; }

        public int Total { get; set; }
    }
}

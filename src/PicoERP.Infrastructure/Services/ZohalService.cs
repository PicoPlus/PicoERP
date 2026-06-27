using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PicoERP.Application.DTOs;
using PicoERP.Application.Interfaces;

namespace PicoERP.Infrastructure.Services;

/// <summary>
/// Calls the Zohal national-identity inquiry web service.
/// Docs: POST https://service.zohal.io/api/v0/services/inquiry/national_identity_inquiry
/// </summary>
public class ZohalService : IZohalService
{
    private const string BaseUrl = "https://service.zohal.io";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISettingsService   _settings;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public ZohalService(IHttpClientFactory httpClientFactory, ISettingsService settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings          = settings;
    }

    public async Task<ZohalIdentityResultDto> InquireAsync(string nationalCode, string birthDate)
    {
        var token = await _settings.GetAsync("Zohal:Token");
        if (string.IsNullOrWhiteSpace(token))
            return new ZohalIdentityResultDto { Error = "توکن سرویس زهل تنظیم نشده است. لطفاً از صفحه تنظیمات آن را وارد کنید." };

        var client  = _httpClientFactory.CreateClient("zohal");
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            BaseUrl + "/api/v0/services/inquiry/national_identity_inquiry");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = JsonSerializer.Serialize(
            new { national_code = nationalCode, birth_date = birthDate },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        HttpResponseMessage resp;
        try   { resp = await client.SendAsync(request); }
        catch (Exception ex)
        {
            return new ZohalIdentityResultDto { Error = $"خطای شبکه: {ex.Message}" };
        }

        var raw = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            // Try to extract message from Zohal's 400 body
            try
            {
                var err = JsonSerializer.Deserialize<ZohalRootResponse>(raw, _json);
                var msg = err?.ResponseBody?.Message ?? err?.ResponseBody?.ErrorCode ?? raw;
                return new ZohalIdentityResultDto { Error = $"خطا از سرویس زهل: {msg}" };
            }
            catch
            {
                return new ZohalIdentityResultDto { Error = $"HTTP {(int)resp.StatusCode}: {raw}" };
            }
        }

        ZohalRootResponse? result;
        try   { result = JsonSerializer.Deserialize<ZohalRootResponse>(raw, _json); }
        catch { return new ZohalIdentityResultDto { Error = "پاسخ نامعتبر از سرویس زهل." }; }

        var data = result?.ResponseBody?.Data;
        if (data == null)
            return new ZohalIdentityResultDto { Error = "داده‌ای در پاسخ سرویس زهل دریافت نشد." };

        return new ZohalIdentityResultDto
        {
            Matched      = data.Matched,
            FirstName    = data.FirstName,
            LastName     = data.LastName,
            FatherName   = data.FatherName,
            Alive        = data.Alive,
            IsDead       = data.IsDead,
            NationalCode = data.NationalCode
        };
    }

    // ── Private deserialization models ───────────────────────────────────────

    private sealed class ZohalRootResponse
    {
        [JsonPropertyName("response_body")]
        public ZohalResponseBody? ResponseBody { get; set; }

        [JsonPropertyName("result")]
        public int Result { get; set; }
    }

    private sealed class ZohalResponseBody
    {
        [JsonPropertyName("data")]
        public ZohalData? Data { get; set; }

        [JsonPropertyName("error_code")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    private sealed class ZohalData
    {
        [JsonPropertyName("matched")]
        public bool Matched { get; set; }

        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }

        [JsonPropertyName("father_name")]
        public string? FatherName { get; set; }

        [JsonPropertyName("alive")]
        public bool? Alive { get; set; }

        [JsonPropertyName("is_dead")]
        public bool? IsDead { get; set; }

        [JsonPropertyName("national_code")]
        public string? NationalCode { get; set; }
    }
}

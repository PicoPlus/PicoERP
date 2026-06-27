using PicoERP.Application.Common;
using PicoERP.Application.DTOs;

namespace PicoERP.Application.Interfaces;

public interface ISmsService
{
    /// <summary>Sends a single SMS to the given phone number. Returns a Result with success/error.</summary>
    Task<Result> SendAsync(string toPhone, string message);

    /// <summary>
    /// Sends a message to the admin phone number stored in settings (Sms:AdminPhone).
    /// Returns Failure when the admin phone is not configured.
    /// </summary>
    Task<Result> SendToAdminAsync(string message);

    /// <summary>Tests the configured API key by sending a short test message to the given phone.</summary>
    Task<Result> TestAsync(string apiKey, string sender, string toPhone);

    /// <summary>
    /// Fetches the list of numbers that belong to the account identified by <paramref name="apiKey"/>
    /// from the IPPanel "List Numbers" endpoint.
    /// Returns an empty list on error or when the key is blank.
    /// </summary>
    Task<IReadOnlyList<string>> GetNumbersAsync(string apiKey);

    // ── Pattern APIs ────────────────────────────────────────────────────────

    /// <summary>Lists all patterns for the account. Returns empty list on error.</summary>
    Task<List<IpPanelPatternDto>> GetPatternsAsync(string apiKey, int page = 1, int perPage = 50);

    /// <summary>Gets a single pattern by its code.</summary>
    Task<IpPanelPatternDto?> GetPatternByCodeAsync(string apiKey, string patternCode);

    /// <summary>Creates a new pattern. Returns the created pattern or null on failure.</summary>
    Task<Result<IpPanelPatternDto>> CreatePatternAsync(string apiKey, CreatePatternDto dto);

    /// <summary>Updates an existing pattern by code.</summary>
    Task<Result<IpPanelPatternDto>> UpdatePatternAsync(string apiKey, string patternCode, CreatePatternDto dto);

    /// <summary>Deletes a pattern by code.</summary>
    Task<Result> DeletePatternAsync(string apiKey, string patternCode);

    /// <summary>
    /// Sends a pattern SMS. <paramref name="variables"/> maps variable name → value.
    /// Returns the IPPanel outbox message ID on success.
    /// </summary>
    Task<Result<string>> SendPatternAsync(string apiKey, string patternCode, string toPhone, Dictionary<string, string> variables, string? fromNumber = null);

    /// <summary>Gets a full outbox report from IPPanel.</summary>
    Task<List<IpPanelOutboxReportDto>> GetOutboxReportAsync(string apiKey, int page = 1, int limit = 20);
}

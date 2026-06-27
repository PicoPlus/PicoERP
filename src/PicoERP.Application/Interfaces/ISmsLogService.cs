using PicoERP.Application.DTOs;

namespace PicoERP.Application.Interfaces;

public interface ISmsLogService
{
    /// <summary>Returns all SMS logs for the given contact phone number, newest first.</summary>
    Task<List<SmsLogDto>> GetLogsForContactAsync(string contactPhone);

    /// <summary>Returns all SMS logs optionally filtered by contact HubSpot ID.</summary>
    Task<List<SmsLogDto>> GetLogsAsync(string? contactHsId = null, int page = 1, int pageSize = 50);

    /// <summary>Persists a new SMS log entry and returns its assigned ID.</summary>
    Task<int> SaveLogAsync(SmsLogDto dto);
}

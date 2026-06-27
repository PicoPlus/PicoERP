using PicoERP.Application.Common;
using PicoERP.Application.DTOs;

namespace PicoERP.Application.Interfaces;

public interface IDailyClosingService
{
    /// <summary>Returns a pre-populated summary for the UI (auto-computed, not yet saved).</summary>
    Task<DailyClosingSummaryDto> GetTodaySummaryAsync(DateTime date);

    Task<List<DailyClosingDto>> GetHistoryAsync(int take = 30);

    Task<Result<DailyClosingDto>> CloseAsync(CreateDailyClosingDto dto, string registeredBy);
}

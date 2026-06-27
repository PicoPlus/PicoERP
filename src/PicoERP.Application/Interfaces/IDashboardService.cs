using PicoERP.Application.DTOs;

namespace PicoERP.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardDataAsync();
}

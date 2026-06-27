using PicoERP.Application.Common;
using PicoERP.Application.DTOs;

namespace PicoERP.Application.Interfaces;

public interface IIncomeService
{
    Task<PagedResult<IncomeDto>> GetPagedAsync(PaginationParams paging, DateTime? from = null, DateTime? to = null, int? categoryId = null);
    Task<IncomeDto?> GetByIdAsync(int id);
    Task<Result<IncomeDto>> CreateAsync(CreateIncomeDto dto, string registeredBy);
    Task<Result<IncomeDto>> UpdateAsync(UpdateIncomeDto dto);
    Task<Result> DeleteAsync(int id);
    Task<decimal> GetTotalAsync(DateTime? from = null, DateTime? to = null);
    Task<List<IncomeCategoryDto>> GetCategoriesAsync();
    Task<List<IncomeDto>> GetRecentAsync(int count = 5);
}

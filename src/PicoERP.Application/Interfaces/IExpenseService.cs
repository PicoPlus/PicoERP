using PicoERP.Application.Common;
using PicoERP.Application.DTOs;

namespace PicoERP.Application.Interfaces;

public interface IExpenseService
{
    Task<PagedResult<ExpenseDto>> GetPagedAsync(PaginationParams paging, DateTime? from = null, DateTime? to = null, int? categoryId = null, Domain.Enums.ExpenseGroup? group = null);
    Task<ExpenseDto?> GetByIdAsync(int id);
    Task<Result<ExpenseDto>> CreateAsync(CreateExpenseDto dto, string registeredBy);
    Task<Result<ExpenseDto>> UpdateAsync(UpdateExpenseDto dto);
    Task<Result> DeleteAsync(int id);
    Task<decimal> GetTotalAsync(DateTime? from = null, DateTime? to = null);
    Task<List<ExpenseCategoryDto>> GetCategoriesAsync();
    Task<List<ExpenseDto>> GetRecentAsync(int count = 5);
}

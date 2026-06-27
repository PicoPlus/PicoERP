using PicoERP.Application.Common;
using PicoERP.Application.DTOs;

namespace PicoERP.Application.Interfaces;

public interface IFinancialAccountService
{
    Task<List<FinancialAccountDto>> GetAllAsync();
    Task<FinancialAccountDto?> GetByIdAsync(int id);
    Task<Result<FinancialAccountDto>> CreateAsync(FinancialAccountDto dto);
    Task<Result<FinancialAccountDto>> UpdateAsync(FinancialAccountDto dto);
    Task<Result> DeleteAsync(int id);
    Task<Result<AccountTransferDto>> TransferAsync(CreateAccountTransferDto dto, string registeredBy);
    Task<List<AccountTransferDto>> GetTransfersAsync(int accountId);
    Task UpdateBalanceAsync(int accountId, decimal amount, bool isIncome);
}

using PicoERP.Application.Common;
using PicoERP.Application.DTOs;

namespace PicoERP.Application.Interfaces;

public interface IBankTransferService
{
    // Receipts
    Task<List<BankTransferReceiptDto>> GetReceiptsAsync(DateTime? from = null, DateTime? to = null);
    Task<BankTransferReceiptDto?> GetReceiptByIdAsync(int id);
    Task<Result<BankTransferReceiptDto>> CreateReceiptAsync(CreateBankTransferReceiptDto dto, string registeredBy);
    Task<Result> DeleteReceiptAsync(int id);

    // Payments linked to a receipt
    Task<Result<BankTransferPaymentDto>> AddPaymentAsync(CreateBankTransferPaymentDto dto, string registeredBy);
    Task<Result> DeletePaymentAsync(int id);

    /// <summary>Sum of all receipts on a given date (used by daily-closing auto-calc).</summary>
    Task<decimal> GetDayReceiptTotalAsync(DateTime date);
}

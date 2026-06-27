using PicoERP.Domain.Common;

namespace PicoERP.Domain.Entities;

/// <summary>
/// Represents an incoming customer payment received via card-to-card (bank transfer).
/// One receipt can be the source of multiple outgoing payments (BankTransferPayments).
/// </summary>
public class BankTransferReceipt : BaseEntity
{
    /// <summary>Transaction / trace ID provided by the bank (شناسه پیگیری).</summary>
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>Name or description of the payer (e.g. customer name).</summary>
    public string PayerName { get; set; } = string.Empty;

    /// <summary>Total amount received from the customer.</summary>
    public decimal Amount { get; set; }

    public DateTime Date { get; set; }

    /// <summary>The bank/card account that received this transfer.</summary>
    public int FinancialAccountId { get; set; }
    public FinancialAccount FinancialAccount { get; set; } = null!;

    public string? Description { get; set; }
    public string? RegisteredBy { get; set; }

    // Navigation — outgoing payments made from this received amount
    public ICollection<BankTransferPayment> Payments { get; set; } = new List<BankTransferPayment>();

    /// <summary>Sum of all linked payments (computed in-memory; not stored).</summary>
    public decimal TotalPaid => Payments.Sum(p => p.Amount);
    public decimal Remaining => Amount - TotalPaid;
}

using PicoERP.Domain.Common;

namespace PicoERP.Domain.Entities;

/// <summary>
/// An outgoing payment made from funds received via a BankTransferReceipt.
/// Multiple payments can be linked to a single receipt.
/// </summary>
public class BankTransferPayment : BaseEntity
{
    /// <summary>The incoming receipt this payment is drawn from.</summary>
    public int ReceiptId { get; set; }
    public BankTransferReceipt Receipt { get; set; } = null!;

    /// <summary>Transaction / trace ID of the outgoing payment (شناسه پیگیری خروجی).</summary>
    public string? TransactionId { get; set; }

    /// <summary>Recipient of the outgoing payment.</summary>
    public string RecipientName { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public DateTime Date { get; set; }

    /// <summary>The bank account the payment was sent FROM.</summary>
    public int? FinancialAccountId { get; set; }
    public FinancialAccount? FinancialAccount { get; set; }

    /// <summary>Purpose / description of this payment (e.g. "پرداخت فاکتور شماره 12").</summary>
    public string? Purpose { get; set; }

    public string? RegisteredBy { get; set; }
}

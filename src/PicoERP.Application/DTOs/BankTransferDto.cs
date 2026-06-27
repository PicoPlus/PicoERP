namespace PicoERP.Application.DTOs;

// ── Incoming customer payment (card-to-card) ─────────────────────────────────

public class BankTransferReceiptDto
{
    public int Id { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string PayerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public int FinancialAccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RegisteredBy { get; set; }
    public List<BankTransferPaymentDto> Payments { get; set; } = new();
    public decimal TotalPaid => Payments.Sum(p => p.Amount);
    public decimal Remaining => Amount - TotalPaid;
    public DateTime CreatedAt { get; set; }
}

public class CreateBankTransferReceiptDto
{
    public string TransactionId { get; set; } = string.Empty;
    public string PayerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public int FinancialAccountId { get; set; }
    public string? Description { get; set; }
}

// ── Outgoing payment drawn from an incoming receipt ──────────────────────────

public class BankTransferPaymentDto
{
    public int Id { get; set; }
    public int ReceiptId { get; set; }
    public string? TransactionId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public int? FinancialAccountId { get; set; }
    public string? AccountName { get; set; }
    public string? Purpose { get; set; }
    public string? RegisteredBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateBankTransferPaymentDto
{
    public int ReceiptId { get; set; }
    public string? TransactionId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public int? FinancialAccountId { get; set; }
    public string? Purpose { get; set; }
}

// ── Daily Closing ─────────────────────────────────────────────────────────────

public class DailyClosingDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public decimal CashOnHand { get; set; }
    public decimal BankTransferIncome { get; set; }
    public decimal PosIncome { get; set; }
    public decimal OnlineIncome { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal Profit { get; set; }
    public string? Notes { get; set; }
    public string? RegisteredBy { get; set; }
    public bool IsFinalized { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateDailyClosingDto
{
    public DateTime Date { get; set; } = DateTime.Today;
    public decimal CashOnHand { get; set; }
    /// <summary>Manual override; if 0 the service auto-sums BankTransferReceipts for the day.</summary>
    public decimal BankTransferIncome { get; set; }
    public decimal PosIncome { get; set; }
    public decimal OnlineIncome { get; set; }
    public string? Notes { get; set; }
}

// ── Smart summary used by the closing UI ─────────────────────────────────────

public class DailyClosingSummaryDto
{
    public DateTime Date { get; set; }
    /// <summary>Total income from all Income records for the day (auto-queried).</summary>
    public decimal TotalIncomeRecorded { get; set; }
    /// <summary>Auto-summed from BankTransferReceipts for the day.</summary>
    public decimal AutoBankTransferTotal { get; set; }
    public decimal TotalExpense { get; set; }
    /// <summary>Per-account breakdown for the day.</summary>
    public List<AccountDaySummaryDto> AccountBreakdown { get; set; } = new();
    public bool IsClosed { get; set; }
}

public class AccountDaySummaryDto
{
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
}

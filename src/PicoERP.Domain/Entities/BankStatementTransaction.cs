using PicoERP.Domain.Common;

namespace PicoERP.Domain.Entities;

/// <summary>
/// Represents a single row imported from a bank statement Excel file.
/// </summary>
public class BankStatementTransaction : BaseEntity
{
    /// <summary>Financial account this statement belongs to.</summary>
    public int FinancialAccountId { get; set; }
    public FinancialAccount FinancialAccount { get; set; } = null!;

    // ── Raw values exactly as they appear in the Excel file ──────────────

    /// <summary>Persian date+time string from the Excel, e.g. "1405/04/05 14:41:48".</summary>
    public string TransactionDateRaw { get; set; } = string.Empty;

    /// <summary>Date portion extracted from TransactionDateRaw (stored as UTC DateTime at midnight).</summary>
    public DateTime TransactionDate { get; set; }

    /// <summary>Time portion extracted from TransactionDateRaw, or null if not present.</summary>
    public TimeSpan? TransactionTime { get; set; }

    /// <summary>Bank document / reference number (شماره سند).</summary>
    public string? DocumentNumber { get; set; }

    /// <summary>Full description from the bank (شرح سند).</summary>
    public string? Description { get; set; }

    /// <summary>Transaction type label from the bank (نوع تراکنش), e.g. "دریافت از کارت".</summary>
    public string? TransactionType { get; set; }

    /// <summary>Amount deposited (واریز). Zero when this is a withdrawal.</summary>
    public decimal DepositAmount { get; set; }

    /// <summary>Amount withdrawn (برداشت). Zero when this is a deposit.</summary>
    public decimal WithdrawalAmount { get; set; }

    /// <summary>Running balance after this transaction (مانده).</summary>
    public decimal Balance { get; set; }

    // ── Import metadata ────────────────────────────────────────────────────

    /// <summary>Name of the importer/parser that produced this record, e.g. "MelatBank".</summary>
    public string ImportSource { get; set; } = string.Empty;

    /// <summary>Gregorian timestamp of the import batch.</summary>
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}

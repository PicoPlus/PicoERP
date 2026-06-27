using PicoERP.Domain.Common;

namespace PicoERP.Domain.Entities;

public class DailyClosing : BaseEntity
{
    public DateTime Date { get; set; }

    // ── Breakdown by payment method ─────────────────────────────────────────
    /// <summary>Physical cash counted in the till at close.</summary>
    public decimal CashOnHand { get; set; }

    /// <summary>Total received via card-to-card bank transfers (auto-summed from BankTransferReceipts).</summary>
    public decimal BankTransferIncome { get; set; }

    /// <summary>Total received via POS / card-reader terminals.</summary>
    public decimal PosIncome { get; set; }

    /// <summary>Total received via online payment gateways.</summary>
    public decimal OnlineIncome { get; set; }

    // ── Legacy / summary fields (kept for backward compat) ──────────────────
    public decimal CashIncome { get; set; }
    public decimal CardIncome { get; set; }
    public decimal BankIncome { get; set; }

    // ── Totals (computed and stored at close time) ──────────────────────────
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal Profit { get; set; }

    public string? Notes { get; set; }
    public string? RegisteredBy { get; set; }
    public bool IsFinalized { get; set; }
}

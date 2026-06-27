namespace PicoERP.Application.DTOs;

// ── Per-row classification chosen by the user in the preview step ─────────────

public enum BankStatementRowAction
{
    Ignore   = 0,
    Income   = 1,
    Expense  = 2,
    Transfer = 3
}

// ── A single split line within a bank row ─────────────────────────────────────

/// <summary>
/// When a single bank transaction needs to be split across multiple
/// income/expense lines (e.g. one withdrawal = rent + salary), the user
/// adds one BankStatementSplitDto per portion.
/// </summary>
public class BankStatementSplitDto
{
    public BankStatementRowAction Action { get; set; } = BankStatementRowAction.Expense;
    public decimal Amount { get; set; }
    public int? CategoryId { get; set; }
    public string? Description { get; set; }
    /// <summary>Target account for Transfer splits.</summary>
    public int? ToAccountId { get; set; }
}

// ── Preview row shown to the user before confirming import ────────────────────

public class BankStatementRowDto
{
    // Raw values from the Excel file
    public string TransactionDateRaw { get; set; } = string.Empty;
    public string? DocumentNumber { get; set; }
    public string? Description { get; set; }
    public string? TransactionType { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal WithdrawalAmount { get; set; }
    public decimal Balance { get; set; }
    public bool IsDuplicate { get; set; }

    // User classification — filled in the preview step before committing
    public BankStatementRowAction Action { get; set; } = BankStatementRowAction.Ignore;
    public int? CategoryId { get; set; }
    /// <summary>Target account when Action = Transfer.</summary>
    public int? ToAccountId { get; set; }

    /// <summary>
    /// When set, the row is split into multiple sub-entries instead of a
    /// single income/expense line.  The splits must sum to ≤ the original amount.
    /// </summary>
    public List<BankStatementSplitDto> Splits { get; set; } = new();

    /// <summary>True when the user has opened the split editor for this row.</summary>
    public bool IsSplitting { get; set; }
}

// ── Request sent from the UI to the service ───────────────────────────────────

public class BankStatementImportRequestDto
{
    /// <summary>Content of the uploaded .xlsx file.</summary>
    public byte[] FileBytes { get; set; } = Array.Empty<byte>();

    /// <summary>Identifies which bank/parser to use, e.g. "MelatBank".</summary>
    public string BankKey { get; set; } = "MelatBank";

    /// <summary>Target financial account to associate imported transactions with.</summary>
    public int FinancialAccountId { get; set; }

    /// <summary>Filter: only import transactions on or after this Persian date string (yyyy/MM/dd).</summary>
    public string? StartDatePersian { get; set; }

    /// <summary>Filter: only import transactions on or before this Persian date string (yyyy/MM/dd).</summary>
    public string? EndDatePersian { get; set; }

    /// <summary>Username of the operator performing the import.</summary>
    public string ImportedBy { get; set; } = string.Empty;
}

// ── Commit request: carries the already-previewed + classified rows ───────────

public class BankStatementCommitRequestDto
{
    /// <summary>Rows from the preview with user-assigned Action and CategoryId.</summary>
    public List<BankStatementRowDto> PreviewedRows { get; set; } = new();

    public string BankKey { get; set; } = string.Empty;
    public int FinancialAccountId { get; set; }
    public string ImportedBy { get; set; } = string.Empty;
}

// ── Result returned after a parse (before commit) ─────────────────────────────

public class BankStatementPreviewDto
{
    public List<BankStatementRowDto> Rows { get; set; } = new();

    public int TotalRowsInFile { get; set; }
    public int ValidRows { get; set; }
    public int DuplicateRows { get; set; }
    public int IgnoredRows { get; set; }

    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
}

// ── Result returned after the confirmed import ────────────────────────────────

public class BankStatementImportResultDto
{
    public bool IsSuccess { get; set; }
    public string? Error { get; set; }

    public int TotalRowsInFile { get; set; }
    public int ValidRows { get; set; }
    public int ImportedRows { get; set; }
    public int DuplicateRows { get; set; }
    public int IgnoredRows { get; set; }

    public int IncomeCount { get; set; }
    public int ExpenseCount { get; set; }
    public int TransferCount { get; set; }

    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
}

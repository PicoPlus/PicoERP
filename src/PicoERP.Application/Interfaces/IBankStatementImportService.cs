using PicoERP.Application.Common;
using PicoERP.Application.DTOs;

namespace PicoERP.Application.Interfaces;

/// <summary>
/// Orchestrates the bank statement import workflow:
///  1. Selects the correct IBankStatementParser by BankKey.
///  2. Applies the Persian date range filter.
///  3. Detects duplicates.
///  4. On confirmation, persists new transactions.
/// </summary>
public interface IBankStatementImportService
{
    /// <summary>Returns all registered bank parser keys and display names.</summary>
    IReadOnlyList<(string Key, string DisplayName)> GetSupportedBanks();

    /// <summary>
    /// Parses the file and returns a preview (does NOT save anything).
    /// The preview includes duplicate flags so the user can review before committing.
    /// </summary>
    Task<Result<BankStatementPreviewDto>> PreviewAsync(BankStatementImportRequestDto request);

    /// <summary>
    /// Persists the non-duplicate rows from a previously confirmed preview.
    /// Runs inside a single database transaction — on any error nothing is written.
    /// </summary>
    Task<Result<BankStatementImportResultDto>> CommitAsync(BankStatementCommitRequestDto request);
}

using PicoERP.Application.DTOs;

namespace PicoERP.Application.Interfaces;

/// <summary>
/// Contract for a bank-specific Excel statement parser.
/// Implement this interface for each bank (Mellat, Saderat, Melli, …).
/// </summary>
public interface IBankStatementParser
{
    /// <summary>Unique key that identifies this parser, e.g. "MelatBank".</summary>
    string BankKey { get; }

    /// <summary>Human-readable display name, e.g. "بانک ملت".</summary>
    string DisplayName { get; }

    /// <summary>
    /// Parses the raw Excel bytes and returns every transaction row found.
    /// The method must:
    ///  - Skip header/summary rows before the transaction table.
    ///  - Ignore blank rows and formatting.
    ///  - Trim all text values.
    ///  - Parse amounts (may contain commas) into decimal.
    ///  - Set TransactionDateRaw to the original cell text.
    /// </summary>
    /// <param name="fileBytes">Raw .xlsx file bytes.</param>
    /// <returns>Parsed rows in file order.</returns>
    Task<List<BankStatementRowDto>> ParseAsync(byte[] fileBytes);
}

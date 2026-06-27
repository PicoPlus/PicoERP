using PicoERP.Application.Common;

namespace PicoERP.Application.Interfaces;

public interface IReportService
{
    Task<byte[]> GenerateIncomePdfAsync(DateTime from, DateTime to);
    Task<byte[]> GenerateExpensePdfAsync(DateTime from, DateTime to);
    Task<byte[]> GenerateProfitPdfAsync(DateTime from, DateTime to);
    Task<byte[]> GenerateSalaryPdfAsync(DateTime from, DateTime to);
    Task<byte[]> GenerateIncomeExcelAsync(DateTime from, DateTime to);
    Task<byte[]> GenerateExpenseExcelAsync(DateTime from, DateTime to);
    Task<byte[]> GenerateProfitExcelAsync(DateTime from, DateTime to);

    /// <summary>Builds a summary SMS text for the given date range and sends it to the admin phone.</summary>
    Task<Common.Result> SendReportSmsAsync(string reportType, DateTime from, DateTime to, string adminPhone);
}

public interface IBackupService
{
    Task<string> CreateBackupAsync();
    Task<Result> RestoreBackupAsync(string backupPath);
    Task<List<BackupInfo>> GetBackupsAsync();
    Task OptimizeDatabaseAsync();
}

public class BackupInfo
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public string FormattedSize => FileSizeBytes < 1024 * 1024
        ? $"{FileSizeBytes / 1024} KB"
        : $"{FileSizeBytes / (1024 * 1024):F1} MB";
}

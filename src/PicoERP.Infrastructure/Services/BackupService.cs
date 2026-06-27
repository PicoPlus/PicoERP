using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PicoERP.Application.Common;
using PicoERP.Application.Interfaces;
using PicoERP.Infrastructure.Persistence;

namespace PicoERP.Infrastructure.Services;

public class BackupService : IBackupService
{
    private readonly AppDbContext _db;
    private readonly string _backupFolder;
    private const string DbFileName = "picoerp.db";

    public BackupService(AppDbContext db)
    {
        _db = db;
        _backupFolder = Path.Combine(AppContext.BaseDirectory, "Backups");
        Directory.CreateDirectory(_backupFolder);
    }

    public async Task<string> CreateBackupAsync()
    {
        var fileName = $"PicoERP_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
        var destPath = Path.Combine(_backupFolder, fileName);

        // Use SQLite online backup API
        var connStr = _db.Database.GetConnectionString()!;
        using var source = new SqliteConnection(connStr);
        await source.OpenAsync();
        using var dest = new SqliteConnection($"Data Source={destPath}");
        await dest.OpenAsync();
        source.BackupDatabase(dest);

        return destPath;
    }

    public async Task<Result> RestoreBackupAsync(string backupPath)
    {
        if (!File.Exists(backupPath))
            return Result.Failure("فایل پشتیبان یافت نشد");

        try
        {
            var connStr = _db.Database.GetConnectionString()!;
            var dbPath = new SqliteConnectionStringBuilder(connStr).DataSource;

            using var source = new SqliteConnection($"Data Source={backupPath}");
            await source.OpenAsync();
            using var dest = new SqliteConnection($"Data Source={dbPath}");
            await dest.OpenAsync();
            source.BackupDatabase(dest);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"خطا در بازیابی: {ex.Message}");
        }
    }

    public Task<List<BackupInfo>> GetBackupsAsync()
    {
        var backups = Directory.GetFiles(_backupFolder, "*.db")
            .OrderByDescending(f => f)
            .Select(f =>
            {
                var fi = new FileInfo(f);
                return new BackupInfo
                {
                    FileName = fi.Name,
                    FilePath = fi.FullName,
                    FileSizeBytes = fi.Length,
                    CreatedAt = fi.CreationTime
                };
            }).ToList();
        return Task.FromResult(backups);
    }

    public async Task OptimizeDatabaseAsync()
    {
        await _db.Database.ExecuteSqlRawAsync("VACUUM;");
        await _db.Database.ExecuteSqlRawAsync("ANALYZE;");
        await _db.Database.ExecuteSqlRawAsync("REINDEX;");
    }
}

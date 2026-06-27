using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PicoERP.Application.Interfaces;
using PicoERP.Infrastructure.Persistence;
using PicoERP.Infrastructure.Services;

namespace PicoERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var dbPath = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=picoerp.db";
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlite(dbPath)
               .EnableSensitiveDataLogging(false)
               .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IIncomeService, IncomeService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IFinancialAccountService, FinancialAccountService>();
        services.AddScoped<ISalaryService, SalaryService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IHubSpotService, HubSpotService>();
        services.AddScoped<ISmsService, SmsService>();
        services.AddScoped<ISmsLogService, SmsLogService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IBankTransferService, BankTransferService>();
        services.AddScoped<IDailyClosingService, DailyClosingService>();
        // Bank statement import — register each parser as IBankStatementParser,
        // then the orchestration service that consumes all registered parsers.
        services.AddScoped<IBankStatementParser, MelatBankStatementParser>();
        services.AddScoped<IBankStatementImportService, BankStatementImportService>();
        services.AddSingleton<IPushNotificationService, PushNotificationService>();
        services.AddHttpClient("hubspot");
        services.AddHttpClient("ippanel");

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Creates the schema from scratch when the DB file is new.
        await db.Database.EnsureCreatedAsync();

        // EnsureCreatedAsync never alters an existing DB, so any rows or tables added
        // after initial creation must be created/inserted manually here.

        // Add HubSpot:ApiKey setting row for existing databases created before this setting was seeded.
        await db.Database.ExecuteSqlRawAsync(@"
            INSERT OR IGNORE INTO ""AppSettings"" (""Key"", ""Value"", ""Description"", ""Group"", ""CreatedAt"", ""IsDeleted"")
            VALUES ('HubSpot:ApiKey', '', 'کلید API هاب‌اسپات (Private App Token)', 'CRM', '2024-01-01T00:00:00.0000000Z', 0);
        ");

        // Add new CRM contact columns for existing databases.
        // SQLite does not support IF NOT EXISTS on ALTER TABLE — swallow the "duplicate column" error instead.
        foreach (var col in new[]
        {
            ("NCode",       "TEXT NULL"),
            ("BirthDate",   "TEXT NULL"),
            ("FatherName",  "TEXT NULL"),
            ("MobilePhone", "TEXT NULL"),
        })
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(
                    $@"ALTER TABLE ""HubSpotContacts"" ADD COLUMN ""{col.Item1}"" {col.Item2};");
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
                when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
            {
                // Column already exists — safe to ignore.
            }
        }

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""BankStatementTransactions"" (
                ""Id""                  INTEGER NOT NULL CONSTRAINT ""PK_BankStatementTransactions"" PRIMARY KEY AUTOINCREMENT,
                ""FinancialAccountId""  INTEGER NOT NULL,
                ""TransactionDateRaw""  TEXT    NOT NULL,
                ""TransactionDate""     TEXT    NOT NULL,
                ""TransactionTime""     TEXT    NULL,
                ""DocumentNumber""      TEXT    NULL,
                ""Description""         TEXT    NULL,
                ""TransactionType""     TEXT    NULL,
                ""DepositAmount""       TEXT    NOT NULL,
                ""WithdrawalAmount""    TEXT    NOT NULL,
                ""Balance""             TEXT    NOT NULL,
                ""ImportSource""        TEXT    NOT NULL,
                ""ImportedAt""          TEXT    NOT NULL,
                ""CreatedAt""           TEXT    NOT NULL,
                ""UpdatedAt""           TEXT    NULL,
                ""CreatedBy""           TEXT    NULL,
                ""UpdatedBy""           TEXT    NULL,
                ""IsDeleted""           INTEGER NOT NULL DEFAULT 0,
                ""DeletedAt""           TEXT    NULL,
                CONSTRAINT ""FK_BankStatementTransactions_FinancialAccounts_FinancialAccountId""
                    FOREIGN KEY (""FinancialAccountId"")
                    REFERENCES ""FinancialAccounts"" (""Id"")
                    ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS ""IX_BankStatementTransactions_FinancialAccountId""
                ON ""BankStatementTransactions"" (""FinancialAccountId"");
        ");

        // SmsLogs table for outbound/inbound SMS tracking
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""SmsLogs"" (
                ""Id""                INTEGER NOT NULL CONSTRAINT ""PK_SmsLogs"" PRIMARY KEY AUTOINCREMENT,
                ""ContactHsId""       TEXT    NULL,
                ""ContactPhone""      TEXT    NOT NULL,
                ""ContactName""       TEXT    NULL,
                ""Direction""         INTEGER NOT NULL DEFAULT 0,
                ""Message""           TEXT    NOT NULL,
                ""IpPanelMessageId""  TEXT    NULL,
                ""SenderNumber""      TEXT    NULL,
                ""PatternCode""       TEXT    NULL,
                ""Status""            TEXT    NULL,
                ""SentAt""            TEXT    NOT NULL,
                ""CreatedAt""         TEXT    NOT NULL,
                ""UpdatedAt""         TEXT    NULL,
                ""CreatedBy""         TEXT    NULL,
                ""UpdatedBy""         TEXT    NULL,
                ""IsDeleted""         INTEGER NOT NULL DEFAULT 0,
                ""DeletedAt""         TEXT    NULL
            );
            CREATE INDEX IF NOT EXISTS ""IX_SmsLogs_ContactHsId""
                ON ""SmsLogs"" (""ContactHsId"");
            CREATE INDEX IF NOT EXISTS ""IX_SmsLogs_ContactPhone""
                ON ""SmsLogs"" (""ContactPhone"");
        ");
    }
}

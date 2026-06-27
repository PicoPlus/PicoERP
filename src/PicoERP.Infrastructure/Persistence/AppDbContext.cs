using Microsoft.EntityFrameworkCore;
using PicoERP.Domain.Entities;

namespace PicoERP.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
    public DbSet<SalaryPayment> SalaryPayments { get; set; }
    public DbSet<IncomeCategory> IncomeCategories { get; set; }
    public DbSet<Income> Incomes { get; set; }
    public DbSet<ExpenseCategory> ExpenseCategories { get; set; }
    public DbSet<Expense> Expenses { get; set; }
    public DbSet<FinancialAccount> FinancialAccounts { get; set; }
    public DbSet<AccountTransfer> AccountTransfers { get; set; }
    public DbSet<Purchase> Purchases { get; set; }
    public DbSet<DailyClosing> DailyClosings { get; set; }
    public DbSet<PendingDeal> PendingDeals { get; set; }
    public DbSet<HubSpotContact> HubSpotContacts { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<AppSetting> AppSettings { get; set; }
    public DbSet<BankTransferReceipt> BankTransferReceipts { get; set; }
    public DbSet<BankTransferPayment> BankTransferPayments { get; set; }
    public DbSet<BankStatementTransaction> BankStatementTransactions { get; set; }
    public DbSet<SmsLog>    SmsLogs    { get; set; }
    public DbSet<ZohalLog>  ZohalLogs  { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global query filter for soft delete
        modelBuilder.Entity<AppUser>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Employee>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<AttendanceRecord>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SalaryPayment>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<IncomeCategory>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Income>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ExpenseCategory>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Expense>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<FinancialAccount>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<AccountTransfer>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Purchase>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<DailyClosing>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<BankTransferReceipt>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<BankTransferPayment>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<BankStatementTransaction>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SmsLog>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ZohalLog>().HasQueryFilter(e => !e.IsDeleted);

        // Precision for decimals
        foreach (var property in modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetColumnType("decimal(18,2)");
        }

        // Unique constraints
        modelBuilder.Entity<HubSpotContact>().HasIndex(c => c.HsObjectId).IsUnique();
        modelBuilder.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<Employee>().HasIndex(e => e.NationalId).IsUnique();
        modelBuilder.Entity<AppSetting>().HasIndex(s => s.Key).IsUnique();

        // AccountTransfer
        modelBuilder.Entity<AccountTransfer>()
            .HasOne(t => t.FromAccount)
            .WithMany(a => a.TransfersFrom)
            .HasForeignKey(t => t.FromAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AccountTransfer>()
            .HasOne(t => t.ToAccount)
            .WithMany(a => a.TransfersTo)
            .HasForeignKey(t => t.ToAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // ExpenseCategory self-reference
        modelBuilder.Entity<ExpenseCategory>()
            .HasOne(c => c.ParentCategory)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Seed default data
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // Default admin user (password: Admin@123)
        modelBuilder.Entity<AppUser>().HasData(new AppUser
        {
            Id = 1,
            Username = "admin",
            PasswordHash = "$2a$11$8YXP1/RC3SIFnWYBq4d5pe9vGtjUYgEKjsY1.2534MbFJaTtTp.pu",
            DisplayName = "مدیر سیستم",
            Role = Domain.Enums.UserRole.SystemAdmin,
            IsActive = true,
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        // Default income categories
        modelBuilder.Entity<IncomeCategory>().HasData(
            new IncomeCategory { Id = 1, Name = "کافی نت", IsActive = true, Icon = "computer", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new IncomeCategory { Id = 2, Name = "چاپ", IsActive = true, Icon = "print", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new IncomeCategory { Id = 3, Name = "اسکن", IsActive = true, Icon = "scanner", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new IncomeCategory { Id = 4, Name = "فتوکپی", IsActive = true, Icon = "copy_all", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new IncomeCategory { Id = 5, Name = "بازی", IsActive = true, Icon = "sports_esports", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new IncomeCategory { Id = 6, Name = "شارژ", IsActive = true, Icon = "sim_card", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new IncomeCategory { Id = 7, Name = "فروش کالا", IsActive = true, Icon = "inventory_2", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new IncomeCategory { Id = 8, Name = "خدمات اینترنت", IsActive = true, Icon = "wifi", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new IncomeCategory { Id = 9, Name = "ثبت نام", IsActive = true, Icon = "how_to_reg", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new IncomeCategory { Id = 10, Name = "سایر", IsActive = true, Icon = "more_horiz", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        // Default expense categories
        modelBuilder.Entity<ExpenseCategory>().HasData(
            // Business
            new ExpenseCategory { Id = 1, Name = "هزینه کاری", Group = Domain.Enums.ExpenseGroup.Business, IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new ExpenseCategory { Id = 2, Name = "اجاره", Group = Domain.Enums.ExpenseGroup.Business, ParentCategoryId = 1, IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new ExpenseCategory { Id = 3, Name = "برق", Group = Domain.Enums.ExpenseGroup.Business, ParentCategoryId = 1, IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new ExpenseCategory { Id = 4, Name = "آب", Group = Domain.Enums.ExpenseGroup.Business, ParentCategoryId = 1, IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new ExpenseCategory { Id = 5, Name = "اینترنت", Group = Domain.Enums.ExpenseGroup.Business, ParentCategoryId = 1, IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new ExpenseCategory { Id = 6, Name = "مواد مصرفی", Group = Domain.Enums.ExpenseGroup.Business, ParentCategoryId = 1, IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new ExpenseCategory { Id = 7, Name = "تعمیرات", Group = Domain.Enums.ExpenseGroup.Business, ParentCategoryId = 1, IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new ExpenseCategory { Id = 8, Name = "تبلیغات", Group = Domain.Enums.ExpenseGroup.Business, ParentCategoryId = 1, IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            // Personal
            new ExpenseCategory { Id = 9, Name = "هزینه شخصی", Group = Domain.Enums.ExpenseGroup.Personal, IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new ExpenseCategory { Id = 10, Name = "برداشت مالک", Group = Domain.Enums.ExpenseGroup.Personal, ParentCategoryId = 9, IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new ExpenseCategory { Id = 11, Name = "خرید شخصی", Group = Domain.Enums.ExpenseGroup.Personal, ParentCategoryId = 9, IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            // Employee
            new ExpenseCategory { Id = 12, Name = "هزینه کارمند", Group = Domain.Enums.ExpenseGroup.Employee, IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new ExpenseCategory { Id = 13, Name = "حقوق", Group = Domain.Enums.ExpenseGroup.Employee, ParentCategoryId = 12, IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new ExpenseCategory { Id = 14, Name = "غذا", Group = Domain.Enums.ExpenseGroup.Employee, ParentCategoryId = 12, IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new ExpenseCategory { Id = 15, Name = "پاداش", Group = Domain.Enums.ExpenseGroup.Employee, ParentCategoryId = 12, IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        // Default settings
        modelBuilder.Entity<AppSetting>().HasData(
            new AppSetting { Id = 1, Key = "BusinessName",      Value = "کافه نت",            Description = "نام کسب و کار",                        Group = "عمومی",   CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppSetting { Id = 2, Key = "Currency",          Value = "Toman",               Description = "واحد پول پیش فرض",                      Group = "مالی",    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppSetting { Id = 3, Key = "UsePersiaNumerals", Value = "true",                Description = "استفاده از اعداد فارسی",                 Group = "نمایش",   CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppSetting { Id = 4, Key = "Theme",             Value = "Light",               Description = "تم برنامه",                             Group = "نمایش",   CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppSetting { Id = 5, Key = "AutoBackup",        Value = "true",                Description = "پشتیبان خودکار",                        Group = "پشتیبان", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppSetting { Id = 6, Key = "Sms:ApiKey",        Value = "YTFiMTBjNzEtOWU5NS00ZjFkLTlkNDYtNDkxYjU1ODY1MDEzNzg5ZTAyNjg4NGZlNWQwNmJjZGMxOTUyZWE3NmViMTc=",
                                                                                               Description = "کلید API آی‌پی‌پنل",                    Group = "پیامک",   CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppSetting { Id = 7, Key = "Sms:Sender",        Value = "+98200010000",        Description = "خط ارسال پیامک (آی‌پی‌پنل)",            Group = "پیامک",   CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppSetting { Id = 8, Key = "Sms:AdminPhone",    Value = "",                    Description = "شماره موبایل مدیر برای دریافت گزارش",   Group = "پیامک",   CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppSetting { Id = 9, Key = "HubSpot:ApiKey",    Value = "",                    Description = "کلید API هاب‌اسپات (Private App Token)", Group = "CRM",     CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}

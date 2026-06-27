using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PicoERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDefaultFinancialAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove the three hard-coded default accounts that were seeded in InitialCreate.
            // Only delete them if they still carry the original seed names and have no linked
            // transactions — safe to skip silently if they were already renamed or deleted.
            migrationBuilder.Sql(@"
                DELETE FROM FinancialAccounts
                WHERE Id IN (1, 2, 3)
                  AND Name IN ('صندوق اصلی', 'بانک ملت', 'کارتخوان سامان')
                  AND IsDeleted = 0
                  AND NOT EXISTS (SELECT 1 FROM Incomes   WHERE FinancialAccountId = FinancialAccounts.Id AND IsDeleted = 0)
                  AND NOT EXISTS (SELECT 1 FROM Expenses  WHERE FinancialAccountId = FinancialAccounts.Id AND IsDeleted = 0)
                  AND NOT EXISTS (SELECT 1 FROM AccountTransfers WHERE (FromAccountId = FinancialAccounts.Id OR ToAccountId = FinancialAccounts.Id) AND IsDeleted = 0)
                  AND NOT EXISTS (SELECT 1 FROM SalaryPayments WHERE FinancialAccountId = FinancialAccounts.Id AND IsDeleted = 0)
                  AND NOT EXISTS (SELECT 1 FROM BankTransferReceipts WHERE FinancialAccountId = FinancialAccounts.Id AND IsDeleted = 0)
                  AND NOT EXISTS (SELECT 1 FROM BankStatementTransactions WHERE FinancialAccountId = FinancialAccounts.Id AND IsDeleted = 0);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No rollback — the user manages their own accounts.
        }
    }
}

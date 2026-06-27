using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PicoERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBankTransferAndDailyClosingExpanded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BankTransferIncome",
                table: "DailyClosings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CashOnHand",
                table: "DailyClosings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OnlineIncome",
                table: "DailyClosings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PosIncome",
                table: "DailyClosings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "BankTransferReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TransactionId = table.Column<string>(type: "TEXT", nullable: false),
                    PayerName = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinancialAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    RegisteredBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransferReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankTransferReceipts_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BankTransferPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReceiptId = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionId = table.Column<string>(type: "TEXT", nullable: true),
                    RecipientName = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinancialAccountId = table.Column<int>(type: "INTEGER", nullable: true),
                    Purpose = table.Column<string>(type: "TEXT", nullable: true),
                    RegisteredBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransferPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankTransferPayments_BankTransferReceipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "BankTransferReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankTransferPayments_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransferPayments_FinancialAccountId",
                table: "BankTransferPayments",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransferPayments_ReceiptId",
                table: "BankTransferPayments",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransferReceipts_FinancialAccountId",
                table: "BankTransferReceipts",
                column: "FinancialAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankTransferPayments");

            migrationBuilder.DropTable(
                name: "BankTransferReceipts");

            migrationBuilder.DropColumn(
                name: "BankTransferIncome",
                table: "DailyClosings");

            migrationBuilder.DropColumn(
                name: "CashOnHand",
                table: "DailyClosings");

            migrationBuilder.DropColumn(
                name: "OnlineIncome",
                table: "DailyClosings");

            migrationBuilder.DropColumn(
                name: "PosIncome",
                table: "DailyClosings");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PicoERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAdminHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$8YXP1/RC3SIFnWYBq4d5pe9vGtjUYgEKjsY1.2534MbFJaTtTp.pu");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$K9Qg.uHYtI5CyLSYgzW0j.NeNuF6NTAKrHkFHcuWo7x2NKtxjBQSG");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPOS_ERP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrintAndPaymentSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnablePaymentCard",
                table: "StoreSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnablePaymentCash",
                table: "StoreSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnablePaymentTransfer",
                table: "StoreSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PrintReceiptAfterSale",
                table: "StoreSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "StoreSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "EnablePaymentCard", "EnablePaymentCash", "EnablePaymentTransfer", "PrintReceiptAfterSale" },
                values: new object[] { true, true, true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnablePaymentCard",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "EnablePaymentCash",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "EnablePaymentTransfer",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "PrintReceiptAfterSale",
                table: "StoreSettings");
        }
    }
}

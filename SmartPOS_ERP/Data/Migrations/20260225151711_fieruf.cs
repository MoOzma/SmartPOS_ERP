using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPOS_ERP.Data.Migrations
{
    /// <inheritdoc />
    public partial class fieruf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "InvoiceId",
                table: "SalesReturns",
                newName: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_OrderId",
                table: "SalesReturns",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_ProductId",
                table: "SalesReturns",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesReturns_Orders_OrderId",
                table: "SalesReturns",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesReturns_Products_ProductId",
                table: "SalesReturns",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesReturns_Orders_OrderId",
                table: "SalesReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesReturns_Products_ProductId",
                table: "SalesReturns");

            migrationBuilder.DropIndex(
                name: "IX_SalesReturns_OrderId",
                table: "SalesReturns");

            migrationBuilder.DropIndex(
                name: "IX_SalesReturns_ProductId",
                table: "SalesReturns");

            migrationBuilder.RenameColumn(
                name: "OrderId",
                table: "SalesReturns",
                newName: "InvoiceId");
        }
    }
}

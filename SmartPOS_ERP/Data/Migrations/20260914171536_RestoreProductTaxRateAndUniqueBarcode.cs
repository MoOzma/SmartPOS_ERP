using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPOS_ERP.Data.Migrations
{
    /// <inheritdoc />
    public partial class RestoreProductTaxRateAndUniqueBarcode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Products
                SET Barcode = NULL
                WHERE Barcode IS NOT NULL AND LTRIM(RTRIM(Barcode)) = '';

                UPDATE Products
                SET Barcode = LEFT(Barcode, 64)
                WHERE Barcode IS NOT NULL AND LEN(Barcode) > 64;

                WITH Duplicates AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (PARTITION BY Barcode ORDER BY Id) AS RowNumber
                    FROM Products
                    WHERE Barcode IS NOT NULL AND Barcode <> N''
                )
                UPDATE p
                SET Barcode = LEFT(p.Barcode + N'-' + CAST(p.Id AS nvarchar(20)), 64)
                FROM Products p
                INNER JOIN Duplicates d ON d.Id = p.Id
                WHERE d.RowNumber > 1;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Barcode",
                table: "Products",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                table: "Products",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode",
                unique: true,
                filter: "[Barcode] IS NOT NULL AND [Barcode] <> N''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Barcode",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "Products");

            migrationBuilder.AlterColumn<string>(
                name: "Barcode",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);
        }
    }
}

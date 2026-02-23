using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPOS_ERP.Data.Migrations
{
    /// <inheritdoc />
    public partial class ii : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Quantity",
                table: "PurchaseDetails",
                newName: "UnitsPerPackage");

            migrationBuilder.RenameColumn(
                name: "CostPrice",
                table: "PurchaseDetails",
                newName: "UnitCost");

            migrationBuilder.AddColumn<decimal>(
                name: "PackageCost",
                table: "PurchaseDetails",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PackageQuantity",
                table: "PurchaseDetails",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalUnits",
                table: "PurchaseDetails",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PackageCost",
                table: "PurchaseDetails");

            migrationBuilder.DropColumn(
                name: "PackageQuantity",
                table: "PurchaseDetails");

            migrationBuilder.DropColumn(
                name: "TotalUnits",
                table: "PurchaseDetails");

            migrationBuilder.RenameColumn(
                name: "UnitsPerPackage",
                table: "PurchaseDetails",
                newName: "Quantity");

            migrationBuilder.RenameColumn(
                name: "UnitCost",
                table: "PurchaseDetails",
                newName: "CostPrice");
        }
    }
}

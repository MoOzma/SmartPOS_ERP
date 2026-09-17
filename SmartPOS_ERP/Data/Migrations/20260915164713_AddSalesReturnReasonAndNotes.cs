using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPOS_ERP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesReturnReasonAndNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "SalesReturns",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "SalesReturns",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "SalesReturns");
        }
    }
}

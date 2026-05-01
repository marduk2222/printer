using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class AddSheetTypeWeightAndOffsetOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "offset_order",
                table: "sheet_types",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "weight",
                table: "sheet_types",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "offset_order",
                table: "printer_billing_sheet_prices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "weight",
                table: "printer_billing_sheet_prices",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "offset_order",
                table: "billing_template_sheet_prices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "weight",
                table: "billing_template_sheet_prices",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "offset_order",
                table: "billing_group_sheet_prices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "weight",
                table: "billing_group_sheet_prices",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "offset_order",
                table: "sheet_types");

            migrationBuilder.DropColumn(
                name: "weight",
                table: "sheet_types");

            migrationBuilder.DropColumn(
                name: "offset_order",
                table: "printer_billing_sheet_prices");

            migrationBuilder.DropColumn(
                name: "weight",
                table: "printer_billing_sheet_prices");

            migrationBuilder.DropColumn(
                name: "offset_order",
                table: "billing_template_sheet_prices");

            migrationBuilder.DropColumn(
                name: "weight",
                table: "billing_template_sheet_prices");

            migrationBuilder.DropColumn(
                name: "offset_order",
                table: "billing_group_sheet_prices");

            migrationBuilder.DropColumn(
                name: "weight",
                table: "billing_group_sheet_prices");
        }
    }
}

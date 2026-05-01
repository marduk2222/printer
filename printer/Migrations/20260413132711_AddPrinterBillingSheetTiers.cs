using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class AddPrinterBillingSheetTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "printer_billing_sheet_tiers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    printer_id = table.Column<int>(type: "int", nullable: false),
                    sheet_type_id = table.Column<int>(type: "int", nullable: false),
                    tier_order = table.Column<int>(type: "int", nullable: false),
                    from_pages = table.Column<int>(type: "int", nullable: false),
                    to_pages = table.Column<int>(type: "int", nullable: true),
                    price = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_printer_billing_sheet_tiers", x => x.id);
                    table.ForeignKey(
                        name: "FK_printer_billing_sheet_tiers_printers_printer_id",
                        column: x => x.printer_id,
                        principalTable: "printers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_printer_billing_sheet_tiers_sheet_types_sheet_type_id",
                        column: x => x.sheet_type_id,
                        principalTable: "sheet_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_printer_billing_sheet_tiers_printer_id",
                table: "printer_billing_sheet_tiers",
                column: "printer_id");

            migrationBuilder.CreateIndex(
                name: "IX_printer_billing_sheet_tiers_sheet_type_id",
                table: "printer_billing_sheet_tiers",
                column: "sheet_type_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "printer_billing_sheet_tiers");
        }
    }
}

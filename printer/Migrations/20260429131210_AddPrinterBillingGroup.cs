using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class AddPrinterBillingGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "billing_group_id",
                table: "printers",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "printer_id",
                table: "invoice_items",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "billing_group_id",
                table: "invoice_items",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_group_summary",
                table: "invoice_items",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "printer_billing_groups",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    billing_partner_id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    page_fee_cycle = table.Column<int>(type: "int", nullable: false),
                    page_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    last_page_billed_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_printer_billing_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_printer_billing_groups_partners_billing_partner_id",
                        column: x => x.billing_partner_id,
                        principalTable: "partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "billing_group_sheet_prices",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    group_id = table.Column<int>(type: "int", nullable: false),
                    sheet_type_id = table.Column<int>(type: "int", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    discount_percent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    free_pages = table.Column<int>(type: "int", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_group_sheet_prices", x => x.id);
                    table.ForeignKey(
                        name: "FK_billing_group_sheet_prices_printer_billing_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "printer_billing_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_billing_group_sheet_prices_sheet_types_sheet_type_id",
                        column: x => x.sheet_type_id,
                        principalTable: "sheet_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "billing_group_sheet_tiers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    group_id = table.Column<int>(type: "int", nullable: false),
                    sheet_type_id = table.Column<int>(type: "int", nullable: false),
                    tier_order = table.Column<int>(type: "int", nullable: false),
                    from_pages = table.Column<int>(type: "int", nullable: false),
                    to_pages = table.Column<int>(type: "int", nullable: true),
                    price = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_group_sheet_tiers", x => x.id);
                    table.ForeignKey(
                        name: "FK_billing_group_sheet_tiers_printer_billing_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "printer_billing_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_billing_group_sheet_tiers_sheet_types_sheet_type_id",
                        column: x => x.sheet_type_id,
                        principalTable: "sheet_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_printers_billing_group_id",
                table: "printers",
                column: "billing_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_billing_group_id",
                table: "invoice_items",
                column: "billing_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_billing_group_sheet_prices_group_id_sheet_type_id",
                table: "billing_group_sheet_prices",
                columns: new[] { "group_id", "sheet_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_group_sheet_prices_sheet_type_id",
                table: "billing_group_sheet_prices",
                column: "sheet_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_billing_group_sheet_tiers_group_id_sheet_type_id_tier_order",
                table: "billing_group_sheet_tiers",
                columns: new[] { "group_id", "sheet_type_id", "tier_order" });

            migrationBuilder.CreateIndex(
                name: "IX_billing_group_sheet_tiers_sheet_type_id",
                table: "billing_group_sheet_tiers",
                column: "sheet_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_printer_billing_groups_billing_partner_id",
                table: "printer_billing_groups",
                column: "billing_partner_id");

            migrationBuilder.CreateIndex(
                name: "IX_printer_billing_groups_is_active",
                table: "printer_billing_groups",
                column: "is_active");

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_items_printer_billing_groups_billing_group_id",
                table: "invoice_items",
                column: "billing_group_id",
                principalTable: "printer_billing_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_printers_printer_billing_groups_billing_group_id",
                table: "printers",
                column: "billing_group_id",
                principalTable: "printer_billing_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_invoice_items_printer_billing_groups_billing_group_id",
                table: "invoice_items");

            migrationBuilder.DropForeignKey(
                name: "FK_printers_printer_billing_groups_billing_group_id",
                table: "printers");

            migrationBuilder.DropTable(
                name: "billing_group_sheet_prices");

            migrationBuilder.DropTable(
                name: "billing_group_sheet_tiers");

            migrationBuilder.DropTable(
                name: "printer_billing_groups");

            migrationBuilder.DropIndex(
                name: "IX_printers_billing_group_id",
                table: "printers");

            migrationBuilder.DropIndex(
                name: "IX_invoice_items_billing_group_id",
                table: "invoice_items");

            migrationBuilder.DropColumn(
                name: "billing_group_id",
                table: "printers");

            migrationBuilder.DropColumn(
                name: "billing_group_id",
                table: "invoice_items");

            migrationBuilder.DropColumn(
                name: "is_group_summary",
                table: "invoice_items");

            migrationBuilder.AlterColumn<int>(
                name: "printer_id",
                table: "invoice_items",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}

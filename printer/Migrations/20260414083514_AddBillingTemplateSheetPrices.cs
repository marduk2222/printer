using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingTemplateSheetPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "billing_template_sheet_prices",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    template_id = table.Column<int>(type: "int", nullable: false),
                    sheet_type_id = table.Column<int>(type: "int", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    discount_percent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    free_pages = table.Column<int>(type: "int", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_template_sheet_prices", x => x.id);
                    table.ForeignKey(
                        name: "FK_billing_template_sheet_prices_billing_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "billing_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_billing_template_sheet_prices_sheet_types_sheet_type_id",
                        column: x => x.sheet_type_id,
                        principalTable: "sheet_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "billing_template_sheet_tiers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    template_id = table.Column<int>(type: "int", nullable: false),
                    sheet_type_id = table.Column<int>(type: "int", nullable: false),
                    tier_order = table.Column<int>(type: "int", nullable: false),
                    from_pages = table.Column<int>(type: "int", nullable: false),
                    to_pages = table.Column<int>(type: "int", nullable: true),
                    price = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_template_sheet_tiers", x => x.id);
                    table.ForeignKey(
                        name: "FK_billing_template_sheet_tiers_billing_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "billing_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_billing_template_sheet_tiers_sheet_types_sheet_type_id",
                        column: x => x.sheet_type_id,
                        principalTable: "sheet_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_billing_template_sheet_prices_sheet_type_id",
                table: "billing_template_sheet_prices",
                column: "sheet_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_billing_template_sheet_prices_template_id",
                table: "billing_template_sheet_prices",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "IX_billing_template_sheet_tiers_sheet_type_id",
                table: "billing_template_sheet_tiers",
                column: "sheet_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_billing_template_sheet_tiers_template_id",
                table: "billing_template_sheet_tiers",
                column: "template_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_template_sheet_prices");

            migrationBuilder.DropTable(
                name: "billing_template_sheet_tiers");
        }
    }
}

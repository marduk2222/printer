using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class AddEinvoiceAllowance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "einvoice_allowances",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    allowance_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    allowance_date = table.Column<DateOnly>(type: "date", nullable: false),
                    einvoice_id = table.Column<int>(type: "int", nullable: false),
                    platform_id = table.Column<int>(type: "int", nullable: true),
                    amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    tax_amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    allowance_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    void_reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    issued_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    void_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_einvoice_allowances", x => x.id);
                    table.ForeignKey(
                        name: "FK_einvoice_allowances_einvoice_platforms_platform_id",
                        column: x => x.platform_id,
                        principalTable: "einvoice_platforms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_einvoice_allowances_einvoices_einvoice_id",
                        column: x => x.einvoice_id,
                        principalTable: "einvoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "einvoice_allowance_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    allowance_id = table.Column<int>(type: "int", nullable: false),
                    description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    subtotal = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_einvoice_allowance_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_einvoice_allowance_items_einvoice_allowances_allowance_id",
                        column: x => x.allowance_id,
                        principalTable: "einvoice_allowances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_einvoice_allowance_items_allowance_id",
                table: "einvoice_allowance_items",
                column: "allowance_id");

            migrationBuilder.CreateIndex(
                name: "IX_einvoice_allowances_allowance_date",
                table: "einvoice_allowances",
                column: "allowance_date");

            migrationBuilder.CreateIndex(
                name: "IX_einvoice_allowances_allowance_number",
                table: "einvoice_allowances",
                column: "allowance_number");

            migrationBuilder.CreateIndex(
                name: "IX_einvoice_allowances_einvoice_id",
                table: "einvoice_allowances",
                column: "einvoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_einvoice_allowances_platform_id",
                table: "einvoice_allowances",
                column: "platform_id");

            migrationBuilder.CreateIndex(
                name: "IX_einvoice_allowances_status",
                table: "einvoice_allowances",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "einvoice_allowance_items");

            migrationBuilder.DropTable(
                name: "einvoice_allowances");
        }
    }
}

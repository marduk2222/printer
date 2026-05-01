using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class AddEinvoiceSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "einvoice_platforms",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    api_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    merchant_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    api_key = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    api_secret = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    extra_params = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_sandbox = table.Column<bool>(type: "bit", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_einvoice_platforms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "einvoices",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    invoice_number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: false),
                    platform_id = table.Column<int>(type: "int", nullable: true),
                    billing_invoice_id = table.Column<int>(type: "int", nullable: true),
                    partner_id = table.Column<int>(type: "int", nullable: false),
                    buyer_tax_id = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    buyer_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    seller_tax_id = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    seller_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    tax_amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    tax_rate = table.Column<int>(type: "int", nullable: false),
                    tax_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_einvoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_einvoices_einvoice_platforms_platform_id",
                        column: x => x.platform_id,
                        principalTable: "einvoice_platforms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_einvoices_invoices_billing_invoice_id",
                        column: x => x.billing_invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_einvoices_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "einvoice_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    einvoice_id = table.Column<int>(type: "int", nullable: false),
                    description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    subtotal = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_einvoice_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_einvoice_items_einvoices_einvoice_id",
                        column: x => x.einvoice_id,
                        principalTable: "einvoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_einvoice_items_einvoice_id",
                table: "einvoice_items",
                column: "einvoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_einvoice_platforms_code",
                table: "einvoice_platforms",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_einvoices_billing_invoice_id",
                table: "einvoices",
                column: "billing_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_einvoices_invoice_date",
                table: "einvoices",
                column: "invoice_date");

            migrationBuilder.CreateIndex(
                name: "IX_einvoices_invoice_number",
                table: "einvoices",
                column: "invoice_number");

            migrationBuilder.CreateIndex(
                name: "IX_einvoices_partner_id",
                table: "einvoices",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "IX_einvoices_platform_id",
                table: "einvoices",
                column: "platform_id");

            migrationBuilder.CreateIndex(
                name: "IX_einvoices_status",
                table: "einvoices",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "einvoice_items");

            migrationBuilder.DropTable(
                name: "einvoices");

            migrationBuilder.DropTable(
                name: "einvoice_platforms");
        }
    }
}

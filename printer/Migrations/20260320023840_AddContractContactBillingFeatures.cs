using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class AddContractContactBillingFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "contract_alert_days",
                table: "printers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "contract_end_date",
                table: "printers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "contract_start_date",
                table: "printers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "deposit",
                table: "printers",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "billing_start_date",
                table: "printer_billing_configs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "last_billed_date",
                table: "printer_billing_configs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fax",
                table: "partners",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "billing_start_date",
                table: "billing_templates",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "partner_billing_infos",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    partner_id = table.Column<int>(type: "int", nullable: false),
                    email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partner_billing_infos", x => x.id);
                    table.ForeignKey(
                        name: "FK_partner_billing_infos_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "partner_contacts",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    partner_id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    mobile = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partner_contacts", x => x.id);
                    table.ForeignKey(
                        name: "FK_partner_contacts_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_printers_contract_end_date",
                table: "printers",
                column: "contract_end_date");

            migrationBuilder.CreateIndex(
                name: "IX_partner_billing_infos_partner_id",
                table: "partner_billing_infos",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "IX_partner_contacts_partner_id",
                table: "partner_contacts",
                column: "partner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "partner_billing_infos");

            migrationBuilder.DropTable(
                name: "partner_contacts");

            migrationBuilder.DropIndex(
                name: "IX_printers_contract_end_date",
                table: "printers");

            migrationBuilder.DropColumn(
                name: "contract_alert_days",
                table: "printers");

            migrationBuilder.DropColumn(
                name: "contract_end_date",
                table: "printers");

            migrationBuilder.DropColumn(
                name: "contract_start_date",
                table: "printers");

            migrationBuilder.DropColumn(
                name: "deposit",
                table: "printers");

            migrationBuilder.DropColumn(
                name: "billing_start_date",
                table: "printer_billing_configs");

            migrationBuilder.DropColumn(
                name: "last_billed_date",
                table: "printer_billing_configs");

            migrationBuilder.DropColumn(
                name: "fax",
                table: "partners");

            migrationBuilder.DropColumn(
                name: "billing_start_date",
                table: "billing_templates");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class AddPrinterUsageRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "replaced_from_printer_id",
                table: "printers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "printer_usage_records",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    printer_id = table.Column<int>(type: "int", nullable: false),
                    partner_id = table.Column<int>(type: "int", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_reason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    replacement_printer_id = table.Column<int>(type: "int", nullable: true),
                    note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_printer_usage_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_printer_usage_records_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partners",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_printer_usage_records_printers_printer_id",
                        column: x => x.printer_id,
                        principalTable: "printers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_printer_usage_records_printers_replacement_printer_id",
                        column: x => x.replacement_printer_id,
                        principalTable: "printers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_printers_replaced_from_printer_id",
                table: "printers",
                column: "replaced_from_printer_id");

            migrationBuilder.CreateIndex(
                name: "IX_printer_usage_records_partner_id_start_date",
                table: "printer_usage_records",
                columns: new[] { "partner_id", "start_date" });

            migrationBuilder.CreateIndex(
                name: "IX_printer_usage_records_printer_id",
                table: "printer_usage_records",
                column: "printer_id");

            migrationBuilder.CreateIndex(
                name: "IX_printer_usage_records_replacement_printer_id",
                table: "printer_usage_records",
                column: "replacement_printer_id");

            migrationBuilder.AddForeignKey(
                name: "FK_printers_printers_replaced_from_printer_id",
                table: "printers",
                column: "replaced_from_printer_id",
                principalTable: "printers",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_printers_printers_replaced_from_printer_id",
                table: "printers");

            migrationBuilder.DropTable(
                name: "printer_usage_records");

            migrationBuilder.DropIndex(
                name: "IX_printers_replaced_from_printer_id",
                table: "printers");

            migrationBuilder.DropColumn(
                name: "replaced_from_printer_id",
                table: "printers");
        }
    }
}

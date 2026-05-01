using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class AddSheetTypeSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sheet_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sheet_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "print_record_values",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    record_id = table.Column<int>(type: "int", nullable: false),
                    sheet_type_id = table.Column<int>(type: "int", nullable: false),
                    value = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_print_record_values", x => x.id);
                    table.ForeignKey(
                        name: "FK_print_record_values_print_records_record_id",
                        column: x => x.record_id,
                        principalTable: "print_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_print_record_values_sheet_types_sheet_type_id",
                        column: x => x.sheet_type_id,
                        principalTable: "sheet_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "printer_billing_sheet_prices",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    printer_id = table.Column<int>(type: "int", nullable: false),
                    sheet_type_id = table.Column<int>(type: "int", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_printer_billing_sheet_prices", x => x.id);
                    table.ForeignKey(
                        name: "FK_printer_billing_sheet_prices_printers_printer_id",
                        column: x => x.printer_id,
                        principalTable: "printers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_printer_billing_sheet_prices_sheet_types_sheet_type_id",
                        column: x => x.sheet_type_id,
                        principalTable: "sheet_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "printer_model_sheet_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    printer_model_id = table.Column<int>(type: "int", nullable: false),
                    sheet_type_id = table.Column<int>(type: "int", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_printer_model_sheet_types", x => x.id);
                    table.ForeignKey(
                        name: "FK_printer_model_sheet_types_printer_models_printer_model_id",
                        column: x => x.printer_model_id,
                        principalTable: "printer_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_printer_model_sheet_types_sheet_types_sheet_type_id",
                        column: x => x.sheet_type_id,
                        principalTable: "sheet_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sheet_type_keys",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    sheet_type_id = table.Column<int>(type: "int", nullable: false),
                    driver_key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sheet_type_keys", x => x.id);
                    table.ForeignKey(
                        name: "FK_sheet_type_keys_sheet_types_sheet_type_id",
                        column: x => x.sheet_type_id,
                        principalTable: "sheet_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_print_record_values_record_id_sheet_type_id",
                table: "print_record_values",
                columns: new[] { "record_id", "sheet_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_print_record_values_sheet_type_id",
                table: "print_record_values",
                column: "sheet_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_printer_billing_sheet_prices_printer_id_sheet_type_id",
                table: "printer_billing_sheet_prices",
                columns: new[] { "printer_id", "sheet_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_printer_billing_sheet_prices_sheet_type_id",
                table: "printer_billing_sheet_prices",
                column: "sheet_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_printer_model_sheet_types_printer_model_id_sheet_type_id",
                table: "printer_model_sheet_types",
                columns: new[] { "printer_model_id", "sheet_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_printer_model_sheet_types_sheet_type_id",
                table: "printer_model_sheet_types",
                column: "sheet_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_sheet_type_keys_driver_key",
                table: "sheet_type_keys",
                column: "driver_key");

            migrationBuilder.CreateIndex(
                name: "IX_sheet_type_keys_sheet_type_id",
                table: "sheet_type_keys",
                column: "sheet_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_sheet_types_name",
                table: "sheet_types",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_sheet_types_sort_order",
                table: "sheet_types",
                column: "sort_order");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "print_record_values");

            migrationBuilder.DropTable(
                name: "printer_billing_sheet_prices");

            migrationBuilder.DropTable(
                name: "printer_model_sheet_types");

            migrationBuilder.DropTable(
                name: "sheet_type_keys");

            migrationBuilder.DropTable(
                name: "sheet_types");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderAndSheetTypeConversion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sheet_type_conversion_rates",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    from_sheet_type_id = table.Column<int>(type: "int", nullable: false),
                    to_sheet_type_id = table.Column<int>(type: "int", nullable: false),
                    ratio = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sheet_type_conversion_rates", x => x.id);
                    table.ForeignKey(
                        name: "FK_sheet_type_conversion_rates_sheet_types_from_sheet_type_id",
                        column: x => x.from_sheet_type_id,
                        principalTable: "sheet_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sheet_type_conversion_rates_sheet_types_to_sheet_type_id",
                        column: x => x.to_sheet_type_id,
                        principalTable: "sheet_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    order_number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    partner_id = table.Column<int>(type: "int", nullable: true),
                    printer_id = table.Column<int>(type: "int", nullable: true),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    assigned_to_id = table.Column<int>(type: "int", nullable: true),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    scheduled_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    completed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_orders_app_users_assigned_to_id",
                        column: x => x.assigned_to_id,
                        principalTable: "app_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_work_orders_app_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "app_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_work_orders_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_printers_printer_id",
                        column: x => x.printer_id,
                        principalTable: "printers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "work_order_images",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    work_order_id = table.Column<int>(type: "int", nullable: false),
                    file_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    file_path = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    caption = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    uploaded_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_order_images_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalTable: "work_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sheet_type_conversion_rates_from_sheet_type_id_to_sheet_type_id",
                table: "sheet_type_conversion_rates",
                columns: new[] { "from_sheet_type_id", "to_sheet_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sheet_type_conversion_rates_is_active",
                table: "sheet_type_conversion_rates",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_sheet_type_conversion_rates_to_sheet_type_id",
                table: "sheet_type_conversion_rates",
                column: "to_sheet_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_images_work_order_id",
                table: "work_order_images",
                column: "work_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_assigned_to_id",
                table: "work_orders",
                column: "assigned_to_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_created_at",
                table: "work_orders",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_created_by_id",
                table: "work_orders",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_order_number",
                table: "work_orders",
                column: "order_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_partner_id",
                table: "work_orders",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_printer_id",
                table: "work_orders",
                column: "printer_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_status",
                table: "work_orders",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sheet_type_conversion_rates");

            migrationBuilder.DropTable(
                name: "work_order_images");

            migrationBuilder.DropTable(
                name: "work_orders");
        }
    }
}

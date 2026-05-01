using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderMultiSelect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_app_users_assigned_to_id",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_printers_printer_id",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_assigned_to_id",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_printer_id",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "assigned_to_id",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "printer_id",
                table: "work_orders");

            migrationBuilder.CreateTable(
                name: "work_order_assignees",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    work_order_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_assignees", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_order_assignees_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_work_order_assignees_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalTable: "work_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_order_printers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    work_order_id = table.Column<int>(type: "int", nullable: false),
                    printer_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_printers", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_order_printers_printers_printer_id",
                        column: x => x.printer_id,
                        principalTable: "printers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_printers_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalTable: "work_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_assignees_user_id",
                table: "work_order_assignees",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_assignees_work_order_id_user_id",
                table: "work_order_assignees",
                columns: new[] { "work_order_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_order_printers_printer_id",
                table: "work_order_printers",
                column: "printer_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_printers_work_order_id_printer_id",
                table: "work_order_printers",
                columns: new[] { "work_order_id", "printer_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_order_assignees");

            migrationBuilder.DropTable(
                name: "work_order_printers");

            migrationBuilder.AddColumn<int>(
                name: "assigned_to_id",
                table: "work_orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "printer_id",
                table: "work_orders",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_assigned_to_id",
                table: "work_orders",
                column: "assigned_to_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_printer_id",
                table: "work_orders",
                column: "printer_id");

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_app_users_assigned_to_id",
                table: "work_orders",
                column: "assigned_to_id",
                principalTable: "app_users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_printers_printer_id",
                table: "work_orders",
                column: "printer_id",
                principalTable: "printers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveClientStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_status",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    partner_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ip = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    last_report = table.Column<DateTime>(type: "datetime2", nullable: false),
                    machine_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    os_info = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    printer_count = table.Column<int>(type: "int", nullable: false),
                    version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_status", x => x.id);
                    table.ForeignKey(
                        name: "FK_client_status_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_client_status_last_report",
                table: "client_status",
                column: "last_report");

            migrationBuilder.CreateIndex(
                name: "IX_client_status_machine_id",
                table: "client_status",
                column: "machine_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_client_status_partner_id",
                table: "client_status",
                column: "partner_id");
        }
    }
}

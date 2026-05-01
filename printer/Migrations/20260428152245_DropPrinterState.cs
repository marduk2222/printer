using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class DropPrinterState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_printers_state",
                table: "printers");

            migrationBuilder.DropColumn(
                name: "state",
                table: "printers");

            migrationBuilder.CreateIndex(
                name: "IX_printers_is_active",
                table: "printers",
                column: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_printers_is_active",
                table: "printers");

            migrationBuilder.AddColumn<string>(
                name: "state",
                table: "printers",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_printers_state",
                table: "printers",
                column: "state");
        }
    }
}

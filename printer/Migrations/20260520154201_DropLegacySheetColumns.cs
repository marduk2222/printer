using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacySheetColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "black_sheets",
                table: "print_records");

            migrationBuilder.DropColumn(
                name: "color_sheets",
                table: "print_records");

            migrationBuilder.DropColumn(
                name: "large_sheets",
                table: "print_records");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "black_sheets",
                table: "print_records",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "color_sheets",
                table: "print_records",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "large_sheets",
                table: "print_records",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}

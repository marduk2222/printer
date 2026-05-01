using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class AddImagePathAndStringCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_printer_models_code",
                table: "printer_models");

            migrationBuilder.AlterColumn<string>(
                name: "code",
                table: "printer_models",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "image_path",
                table: "printer_models",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_path",
                table: "brands",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_printer_models_code",
                table: "printer_models",
                column: "code",
                unique: true,
                filter: "[code] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_printer_models_code",
                table: "printer_models");

            migrationBuilder.DropColumn(
                name: "image_path",
                table: "printer_models");

            migrationBuilder.DropColumn(
                name: "image_path",
                table: "brands");

            migrationBuilder.AlterColumn<int>(
                name: "code",
                table: "printer_models",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_printer_models_code",
                table: "printer_models",
                column: "code",
                unique: true);
        }
    }
}

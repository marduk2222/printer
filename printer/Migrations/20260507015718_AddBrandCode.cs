using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "brands",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_brands_code",
                table: "brands",
                column: "code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_brands_code",
                table: "brands");

            migrationBuilder.DropColumn(
                name: "code",
                table: "brands");
        }
    }
}

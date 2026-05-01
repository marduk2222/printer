using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class ReplacePrinterVendorWithBrandId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "vendor",
                table: "printers");

            migrationBuilder.AddColumn<int>(
                name: "brand_id",
                table: "printers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_printers_brand_id",
                table: "printers",
                column: "brand_id");

            migrationBuilder.AddForeignKey(
                name: "FK_printers_brands_brand_id",
                table: "printers",
                column: "brand_id",
                principalTable: "brands",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_printers_brands_brand_id",
                table: "printers");

            migrationBuilder.DropIndex(
                name: "IX_printers_brand_id",
                table: "printers");

            migrationBuilder.DropColumn(
                name: "brand_id",
                table: "printers");

            migrationBuilder.AddColumn<string>(
                name: "vendor",
                table: "printers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}

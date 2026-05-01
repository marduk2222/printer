using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class AddEinvoiceFieldMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "einvoice_field_mappings",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    platform_id = table.Column<int>(type: "int", nullable: false),
                    field_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    api_param_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    default_value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    format = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    is_required = table.Column<bool>(type: "bit", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_einvoice_field_mappings", x => x.id);
                    table.ForeignKey(
                        name: "FK_einvoice_field_mappings_einvoice_platforms_platform_id",
                        column: x => x.platform_id,
                        principalTable: "einvoice_platforms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_einvoice_field_mappings_platform_id_field_code",
                table: "einvoice_field_mappings",
                columns: new[] { "platform_id", "field_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "einvoice_field_mappings");
        }
    }
}

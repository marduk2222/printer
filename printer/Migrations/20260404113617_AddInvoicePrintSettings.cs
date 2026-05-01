using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicePrintSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invoice_print_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    template_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    company_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    company_slogan = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    company_address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    company_phone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    company_email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    company_website = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    company_logo_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    bank_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    bank_branch = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    bank_account = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    payment_terms = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    footer_note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_print_settings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_print_settings_template_code",
                table: "invoice_print_settings",
                column: "template_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoice_print_settings");
        }
    }
}

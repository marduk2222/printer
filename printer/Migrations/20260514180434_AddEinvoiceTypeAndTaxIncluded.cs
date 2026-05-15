using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class AddEinvoiceTypeAndTaxIncluded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "invoice_type",
                table: "einvoices",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "B2C");

            // 既有列：有 buyer_tax_id 的設成 B2B，其餘維持 B2C
            migrationBuilder.Sql("UPDATE einvoices SET invoice_type = 'B2B' WHERE buyer_tax_id IS NOT NULL AND buyer_tax_id <> '';");

            migrationBuilder.AddColumn<bool>(
                name: "tax_included",
                table: "einvoices",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "invoice_type",
                table: "einvoices");

            migrationBuilder.DropColumn(
                name: "tax_included",
                table: "einvoices");
        }
    }
}

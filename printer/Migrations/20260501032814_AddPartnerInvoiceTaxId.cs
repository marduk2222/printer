using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerInvoiceTaxId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "invoice_tax_id",
                table: "partners",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // 既有客戶 backfill：把 vat / name 拷貝到 invoice_tax_id / invoice_title，
            // 讓帳單/發票直接以 invoice_* 欄位為主（之後使用者可在發票資訊獨立修改）
            migrationBuilder.Sql("UPDATE partners SET invoice_tax_id = vat WHERE invoice_tax_id IS NULL AND vat IS NOT NULL");
            migrationBuilder.Sql("UPDATE partners SET invoice_title = name WHERE invoice_title IS NULL AND name IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "invoice_tax_id",
                table: "partners");
        }
    }
}

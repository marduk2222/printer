using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class RoundEinvoiceAmountsToInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 把既有資料中的金額尾數捨入到整數，避免畫面出現 12,380.95 之類的數字
            migrationBuilder.Sql("UPDATE einvoice_items SET unit_price = ROUND(unit_price, 0), subtotal = ROUND(subtotal, 0);");
            migrationBuilder.Sql("UPDATE einvoices SET amount = ROUND(amount, 0), tax_amount = ROUND(tax_amount, 0), total_amount = ROUND(total_amount, 0);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // no-op：四捨五入無法復原
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class SyncEinvoiceAmountsFromItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 修正歷史資料：把 einvoices.amount/tax_amount/total_amount 重新從 items 加總算出
            // 過去某些操作（手動編輯 / 多次儲存）導致 amount 與 sum(items.subtotal) 不一致
            migrationBuilder.Sql("UPDATE einvoices SET amount = ISNULL((SELECT SUM(subtotal) FROM einvoice_items WHERE einvoice_id = einvoices.id), 0);");
            migrationBuilder.Sql("UPDATE einvoices SET tax_amount = CASE WHEN tax_type = 'taxable' THEN ROUND(amount * tax_rate / 100.0, 0) ELSE 0 END;");
            migrationBuilder.Sql("UPDATE einvoices SET total_amount = amount + tax_amount;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // no-op：無法復原歷史值
        }
    }
}

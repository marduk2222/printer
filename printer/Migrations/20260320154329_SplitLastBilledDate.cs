using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class SplitLastBilledDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "last_billed_date",
                table: "printer_billing_configs",
                newName: "last_page_billed_date");

            migrationBuilder.AddColumn<DateOnly>(
                name: "last_monthly_billed_date",
                table: "printer_billing_configs",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_monthly_billed_date",
                table: "printer_billing_configs");

            migrationBuilder.RenameColumn(
                name: "last_page_billed_date",
                table: "printer_billing_configs",
                newName: "last_billed_date");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingGroupMonthlyFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "last_monthly_billed_date",
                table: "printer_billing_groups",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "monthly_fee",
                table: "printer_billing_groups",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "monthly_fee_cycle",
                table: "printer_billing_groups",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "monthly_start_date",
                table: "printer_billing_groups",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_monthly_billed_date",
                table: "printer_billing_groups");

            migrationBuilder.DropColumn(
                name: "monthly_fee",
                table: "printer_billing_groups");

            migrationBuilder.DropColumn(
                name: "monthly_fee_cycle",
                table: "printer_billing_groups");

            migrationBuilder.DropColumn(
                name: "monthly_start_date",
                table: "printer_billing_groups");
        }
    }
}

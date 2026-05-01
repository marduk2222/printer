using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class SplitBillingStartDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "billing_start_date",
                table: "printer_billing_configs",
                newName: "page_start_date");

            migrationBuilder.RenameColumn(
                name: "billing_start_date",
                table: "billing_templates",
                newName: "page_start_date");

            migrationBuilder.AddColumn<DateOnly>(
                name: "monthly_start_date",
                table: "printer_billing_configs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "monthly_start_date",
                table: "billing_templates",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "monthly_start_date",
                table: "printer_billing_configs");

            migrationBuilder.DropColumn(
                name: "monthly_start_date",
                table: "billing_templates");

            migrationBuilder.RenameColumn(
                name: "page_start_date",
                table: "printer_billing_configs",
                newName: "billing_start_date");

            migrationBuilder.RenameColumn(
                name: "page_start_date",
                table: "billing_templates",
                newName: "billing_start_date");
        }
    }
}

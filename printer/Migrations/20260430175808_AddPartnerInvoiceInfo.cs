using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerInvoiceInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "invoice_carrier_id",
                table: "partners",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_carrier_type",
                table: "partners",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_default_tax_type",
                table: "partners",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_donation_code",
                table: "partners",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_email",
                table: "partners",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_note",
                table: "partners",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_title",
                table: "partners",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "buyer_email",
                table: "einvoices",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "carrier_type",
                table: "einvoices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "carrier_id",
                table: "einvoices",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "donation_code",
                table: "einvoices",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "invoice_carrier_id",
                table: "partners");

            migrationBuilder.DropColumn(
                name: "invoice_carrier_type",
                table: "partners");

            migrationBuilder.DropColumn(
                name: "invoice_default_tax_type",
                table: "partners");

            migrationBuilder.DropColumn(
                name: "invoice_donation_code",
                table: "partners");

            migrationBuilder.DropColumn(
                name: "invoice_email",
                table: "partners");

            migrationBuilder.DropColumn(
                name: "invoice_note",
                table: "partners");

            migrationBuilder.DropColumn(
                name: "invoice_title",
                table: "partners");

            migrationBuilder.DropColumn(
                name: "buyer_email",
                table: "einvoices");

            migrationBuilder.DropColumn(
                name: "carrier_type",
                table: "einvoices");

            migrationBuilder.DropColumn(
                name: "carrier_id",
                table: "einvoices");

            migrationBuilder.DropColumn(
                name: "donation_code",
                table: "einvoices");
        }
    }
}

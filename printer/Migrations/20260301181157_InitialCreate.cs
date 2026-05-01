using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace printer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "billing_templates",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    monthly_fee = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    page_method = table.Column<int>(type: "int", nullable: false),
                    price_per_black = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    price_per_color = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    price_per_large = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    discount_percent_black = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    discount_percent_color = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    discount_percent_large = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    free_black_pages = table.Column<int>(type: "int", nullable: false),
                    free_color_pages = table.Column<int>(type: "int", nullable: false),
                    free_large_pages = table.Column<int>(type: "int", nullable: false),
                    monthly_fee_cycle = table.Column<int>(type: "int", nullable: false),
                    page_fee_cycle = table.Column<int>(type: "int", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "brands",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brands", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "partners",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    partner_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    is_company = table.Column<bool>(type: "bit", nullable: false),
                    vat = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    mobile = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    contact_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    state = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partners", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_modules",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    is_enabled = table.Column<bool>(type: "bit", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    menu_path = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    menu_controller = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    menu_action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_modules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "printer_models",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    brand_id = table.Column<int>(type: "int", nullable: false),
                    feature = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    verify = table.Column<bool>(type: "bit", nullable: false),
                    state = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_printer_models", x => x.id);
                    table.ForeignKey(
                        name: "FK_printer_models_brands_brand_id",
                        column: x => x.brand_id,
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "client_status",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    machine_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    partner_id = table.Column<int>(type: "int", nullable: true),
                    version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ip = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    os_info = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    printer_count = table.Column<int>(type: "int", nullable: false),
                    last_report = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_status", x => x.id);
                    table.ForeignKey(
                        name: "FK_client_status_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    invoice_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    partner_id = table.Column<int>(type: "int", nullable: false),
                    billing_period = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    tax_amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    grand_total = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    confirmed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    paid_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoices_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "partner_modules",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    partner_id = table.Column<int>(type: "int", nullable: false),
                    module_id = table.Column<int>(type: "int", nullable: false),
                    is_enabled = table.Column<bool>(type: "bit", nullable: false),
                    enabled_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partner_modules", x => x.id);
                    table.ForeignKey(
                        name: "FK_partner_modules_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_partner_modules_system_modules_module_id",
                        column: x => x.module_id,
                        principalTable: "system_modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "printers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    partner_id = table.Column<int>(type: "int", nullable: true),
                    partner_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    model_id = table.Column<int>(type: "int", nullable: true),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    printer_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    serial_number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ip = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    mac = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    state = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    toner_black = table.Column<float>(type: "real", nullable: true),
                    toner_cyan = table.Column<float>(type: "real", nullable: true),
                    toner_magenta = table.Column<float>(type: "real", nullable: true),
                    toner_yellow = table.Column<float>(type: "real", nullable: true),
                    toner_waste = table.Column<float>(type: "real", nullable: true),
                    drum_black = table.Column<float>(type: "real", nullable: true),
                    drum_cyan = table.Column<float>(type: "real", nullable: true),
                    drum_magenta = table.Column<float>(type: "real", nullable: true),
                    drum_yellow = table.Column<float>(type: "real", nullable: true),
                    toner_black_alert_threshold = table.Column<int>(type: "int", nullable: false),
                    toner_cyan_alert_threshold = table.Column<int>(type: "int", nullable: false),
                    toner_magenta_alert_threshold = table.Column<int>(type: "int", nullable: false),
                    toner_yellow_alert_threshold = table.Column<int>(type: "int", nullable: false),
                    drum_black_alert_threshold = table.Column<int>(type: "int", nullable: false),
                    drum_cyan_alert_threshold = table.Column<int>(type: "int", nullable: false),
                    drum_magenta_alert_threshold = table.Column<int>(type: "int", nullable: false),
                    drum_yellow_alert_threshold = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_printers", x => x.id);
                    table.ForeignKey(
                        name: "FK_printers_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_printers_printer_models_model_id",
                        column: x => x.model_id,
                        principalTable: "printer_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "alert_records",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    printer_id = table.Column<int>(type: "int", nullable: false),
                    code = table.Column<int>(type: "int", nullable: false),
                    message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    state = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_alert_records_printers_printer_id",
                        column: x => x.printer_id,
                        principalTable: "printers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invoice_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    invoice_id = table.Column<int>(type: "int", nullable: false),
                    printer_id = table.Column<int>(type: "int", nullable: false),
                    billing_type = table.Column<int>(type: "int", nullable: false),
                    description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    black_pages = table.Column<int>(type: "int", nullable: false),
                    color_pages = table.Column<int>(type: "int", nullable: false),
                    large_pages = table.Column<int>(type: "int", nullable: false),
                    monthly_fee = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    page_fee = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    subtotal = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoice_items_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_invoice_items_printers_printer_id",
                        column: x => x.printer_id,
                        principalTable: "printers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "print_records",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    printer_id = table.Column<int>(type: "int", nullable: false),
                    partner_id = table.Column<int>(type: "int", nullable: true),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    black_sheets = table.Column<int>(type: "int", nullable: false),
                    color_sheets = table.Column<int>(type: "int", nullable: false),
                    large_sheets = table.Column<int>(type: "int", nullable: false),
                    state = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    count = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_print_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_print_records_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_print_records_printers_printer_id",
                        column: x => x.printer_id,
                        principalTable: "printers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "printer_billing_configs",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    printer_id = table.Column<int>(type: "int", nullable: false),
                    is_enabled = table.Column<bool>(type: "bit", nullable: false),
                    monthly_fee = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    page_method = table.Column<int>(type: "int", nullable: false),
                    price_per_black = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    price_per_color = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    price_per_large = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    discount_percent_black = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    discount_percent_color = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    discount_percent_large = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    free_black_pages = table.Column<int>(type: "int", nullable: false),
                    free_color_pages = table.Column<int>(type: "int", nullable: false),
                    free_large_pages = table.Column<int>(type: "int", nullable: false),
                    monthly_fee_cycle = table.Column<int>(type: "int", nullable: false),
                    page_fee_cycle = table.Column<int>(type: "int", nullable: false),
                    note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_printer_billing_configs", x => x.id);
                    table.ForeignKey(
                        name: "FK_printer_billing_configs_printers_printer_id",
                        column: x => x.printer_id,
                        principalTable: "printers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "billing_tiers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    billing_config_id = table.Column<int>(type: "int", nullable: true),
                    billing_template_id = table.Column<int>(type: "int", nullable: true),
                    tier_type = table.Column<int>(type: "int", nullable: false),
                    tier_order = table.Column<int>(type: "int", nullable: false),
                    from_pages = table.Column<int>(type: "int", nullable: false),
                    to_pages = table.Column<int>(type: "int", nullable: true),
                    price = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_tiers", x => x.id);
                    table.ForeignKey(
                        name: "FK_billing_tiers_billing_templates_billing_template_id",
                        column: x => x.billing_template_id,
                        principalTable: "billing_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_billing_tiers_printer_billing_configs_billing_config_id",
                        column: x => x.billing_config_id,
                        principalTable: "printer_billing_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alert_records_printer_id_state",
                table: "alert_records",
                columns: new[] { "printer_id", "state" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_records_state",
                table: "alert_records",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "IX_billing_templates_is_active",
                table: "billing_templates",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_billing_templates_name",
                table: "billing_templates",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_billing_tiers_billing_config_id_tier_type_tier_order",
                table: "billing_tiers",
                columns: new[] { "billing_config_id", "tier_type", "tier_order" });

            migrationBuilder.CreateIndex(
                name: "IX_billing_tiers_billing_template_id_tier_type_tier_order",
                table: "billing_tiers",
                columns: new[] { "billing_template_id", "tier_type", "tier_order" });

            migrationBuilder.CreateIndex(
                name: "IX_brands_name",
                table: "brands",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_client_status_last_report",
                table: "client_status",
                column: "last_report");

            migrationBuilder.CreateIndex(
                name: "IX_client_status_machine_id",
                table: "client_status",
                column: "machine_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_client_status_partner_id",
                table: "client_status",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_invoice_id_printer_id",
                table: "invoice_items",
                columns: new[] { "invoice_id", "printer_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_printer_id",
                table: "invoice_items",
                column: "printer_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_invoice_number",
                table: "invoices",
                column: "invoice_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_partner_id_billing_period",
                table: "invoices",
                columns: new[] { "partner_id", "billing_period" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_status",
                table: "invoices",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_partner_modules_module_id",
                table: "partner_modules",
                column: "module_id");

            migrationBuilder.CreateIndex(
                name: "IX_partner_modules_partner_id_module_id",
                table: "partner_modules",
                columns: new[] { "partner_id", "module_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_partners_code",
                table: "partners",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_partners_name",
                table: "partners",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_partners_state",
                table: "partners",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "IX_print_records_date",
                table: "print_records",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "IX_print_records_partner_id",
                table: "print_records",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "IX_print_records_printer_id_date_state",
                table: "print_records",
                columns: new[] { "printer_id", "date", "state" });

            migrationBuilder.CreateIndex(
                name: "IX_printer_billing_configs_printer_id",
                table: "printer_billing_configs",
                column: "printer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_printer_models_brand_id",
                table: "printer_models",
                column: "brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_printer_models_code",
                table: "printer_models",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_printer_models_name",
                table: "printer_models",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_printer_models_state",
                table: "printer_models",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "IX_printers_code",
                table: "printers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_printers_model_id",
                table: "printers",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "IX_printers_partner_id",
                table: "printers",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "IX_printers_serial_number",
                table: "printers",
                column: "serial_number");

            migrationBuilder.CreateIndex(
                name: "IX_printers_state",
                table: "printers",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "IX_system_modules_code",
                table: "system_modules",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_modules_sort_order",
                table: "system_modules",
                column: "sort_order");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_records");

            migrationBuilder.DropTable(
                name: "billing_tiers");

            migrationBuilder.DropTable(
                name: "client_status");

            migrationBuilder.DropTable(
                name: "invoice_items");

            migrationBuilder.DropTable(
                name: "partner_modules");

            migrationBuilder.DropTable(
                name: "print_records");

            migrationBuilder.DropTable(
                name: "billing_templates");

            migrationBuilder.DropTable(
                name: "printer_billing_configs");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "system_modules");

            migrationBuilder.DropTable(
                name: "printers");

            migrationBuilder.DropTable(
                name: "partners");

            migrationBuilder.DropTable(
                name: "printer_models");

            migrationBuilder.DropTable(
                name: "brands");
        }
    }
}

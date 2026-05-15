using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;
using printer.Middleware;
using printer.Services;
using printer.Services.Impl;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Add Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

// Add DbContext with SQL Server
builder.Services.AddDbContext<PrinterDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register API services
builder.Services.AddScoped<IPrinterService, PrinterService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IVersionService, VersionService>();
builder.Services.AddScoped<IBrandService, BrandService>();
builder.Services.AddScoped<IFileService, FileService>();

// Register module, billing, and permission services
builder.Services.AddScoped<IModuleService, ModuleService>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IWorkOrderService, WorkOrderService>();

// 發票平台 providers
builder.Services.AddHttpClient();
// ECPay 子 provider（也以具體型別註冊，給 EcpayProvider 注入）
builder.Services.AddScoped<printer.Services.Invoicing.Providers.EcpayB2CProvider>();
builder.Services.AddScoped<printer.Services.Invoicing.Providers.EcpayB2BProvider>();
builder.Services.AddScoped<printer.Services.Invoicing.IEinvoiceProvider>(sp => sp.GetRequiredService<printer.Services.Invoicing.Providers.EcpayB2CProvider>());
builder.Services.AddScoped<printer.Services.Invoicing.IEinvoiceProvider>(sp => sp.GetRequiredService<printer.Services.Invoicing.Providers.EcpayB2BProvider>());
// 統一的 ecpay：依 BuyerTaxId 自動分派 B2C/B2B
builder.Services.AddScoped<printer.Services.Invoicing.IEinvoiceProvider, printer.Services.Invoicing.Providers.EcpayProvider>();
builder.Services.AddScoped<printer.Services.Invoicing.IEinvoiceProvider, printer.Services.Invoicing.Providers.EzpayProvider>();
builder.Services.AddScoped<printer.Services.Invoicing.IEinvoiceProvider, printer.Services.Invoicing.Providers.GatewebProvider>();
builder.Services.AddScoped<printer.Services.Invoicing.IEinvoiceProviderFactory, printer.Services.Invoicing.EinvoiceProviderFactory>();

// Add Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Printer Cloud Meter API", Version = "v1" });
});

var app = builder.Build();

// Auto migrate database and initialize default modules
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PrinterDbContext>();
    db.Database.Migrate();

    // Initialize default system modules
    var moduleService = scope.ServiceProvider.GetRequiredService<IModuleService>();
    await moduleService.InitializeDefaultModulesAsync();

    // Initialize default permissions
    var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();
    await permissionService.InitializeDefaultPermissionsAsync();

    // Initialize invoice print settings
    if (!db.InvoicePrintSettings.Any())
    {
        db.InvoicePrintSettings.Add(new printer.Data.Entities.InvoicePrintSettings { TemplateCode = "classic" });
        await db.SaveChangesAsync();
    }

    // Initialize default admin user
    if (!db.AppUsers.Any())
    {
        db.AppUsers.Add(new AppUser
        {
            Username = "admin",
            PasswordHash = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes("admin"))),
            DisplayName = "系統管理員",
            Role = "admin",
            IsActive = true
        });
        await db.SaveChangesAsync();
    }

    // 初始化預設廠牌（OID 分派用代碼，需與 printer_info Counter 分支對齊）
    if (!db.Brands.Any())
    {
        db.Brands.AddRange(new List<Brand>
        {
            new() { Code = "common",    Name = "Common",    SortOrder = 1 },
            new() { Code = "ricoh",     Name = "Ricoh",     SortOrder = 2 },
            new() { Code = "ricoh_imc", Name = "Ricoh IMC", SortOrder = 3 },
            new() { Code = "xerox",     Name = "Xerox",     SortOrder = 4 },
            new() { Code = "toshiba",   Name = "Toshiba",   SortOrder = 5 },
            new() { Code = "kyocera",   Name = "Kyocera",   SortOrder = 6 },
        });
        await db.SaveChangesAsync();
    }

    // 初始化預設張數類型 + 對應四維 driver_key
    // driver_key 編碼：{output}.{category}.{color}.{size}
    // 同一張數類型可對應多組 driver_key（依規格累加）
    if (!db.SheetTypes.Any())
    {
        // 列印類別：列印/影印/傳真/網路/清單；色彩：color_full/duotone/mono 視為彩色
        var printCategories = new[] { "print", "copy", "fax", "network", "list" };
        var colorTones      = new[] { "color_full", "duotone", "mono" };

        string[] KeysFor(string output, IEnumerable<string> categories, IEnumerable<string> colors, string size)
            => categories.SelectMany(c => colors.Select(col => $"{output}.{c}.{col}.{size}")).ToArray();

        var defaults = new List<(string Name, int SortOrder, string[] Keys)>
        {
            ("黑白",       1, KeysFor("printed", printCategories, new[] { "black" },        "normal")),
            ("彩色",       2, KeysFor("printed", printCategories, colorTones,               "normal")),
            ("黑白大張",   3, KeysFor("printed", printCategories, new[] { "black" },        "large")),
            ("彩色大張",   4, KeysFor("printed", printCategories, colorTones,               "large")),
            ("掃描黑白",   5, KeysFor("scanned", new[] { "scan" }, new[] { "black" },       "normal")),
            ("掃描彩色",   6, KeysFor("scanned", new[] { "scan" }, colorTones,              "normal")),
            ("掃描黑白大張", 7, KeysFor("scanned", new[] { "scan" }, new[] { "black" },     "large")),
            ("掃描彩色大張", 8, KeysFor("scanned", new[] { "scan" }, colorTones,            "large")),
        };

        foreach (var (name, sortOrder, keys) in defaults)
        {
            var st = new SheetType { Name = name, SortOrder = sortOrder };
            foreach (var k in keys) st.Keys.Add(new SheetTypeKey { DriverKey = k });
            db.SheetTypes.Add(st);
        }
        await db.SaveChangesAsync();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Module check middleware
app.UseModuleCheck();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();

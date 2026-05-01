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

    // 初始化預設張數類型
    if (!db.SheetTypes.Any())
    {
        var defaultSheetTypes = new List<SheetType>
        {
            new() { Name = "黑白",     SortOrder = 1 },
            new() { Name = "彩色",     SortOrder = 2 },
            new() { Name = "黑白大張", SortOrder = 3 },
            new() { Name = "彩色大張", SortOrder = 4 },
            new() { Name = "單面",     SortOrder = 5 },
            new() { Name = "雙面",     SortOrder = 6 },
        };
        db.SheetTypes.AddRange(defaultSheetTypes);
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

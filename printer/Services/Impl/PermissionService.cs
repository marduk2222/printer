using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;

namespace printer.Services.Impl;

public class PermissionService : IPermissionService
{
    private readonly PrinterDbContext _context;

    private static readonly List<FeatureDefinition> Features = new()
    {
        new() { Code = "home", Name = "首頁", Controller = "Home" },
        new() { Code = "partner", Name = "客戶管理", Controller = "Partner" },
        new() { Code = "printer", Name = "事務機管理", Controller = "Printer" },
        new() { Code = "print_record", Name = "抄表記錄", Controller = "PrintRecord" },
        new() { Code = "work_order", Name = "派工管理", Controller = "WorkOrder" },
        new() { Code = "billing", Name = "計費系統", Controller = "BillingConfig" },
        new() { Code = "invoice", Name = "帳單管理", Controller = "Invoice" },
        new() { Code = "billing_report", Name = "計費報表", Controller = "BillingReport" },
        new() { Code = "system_brand", Name = "廠牌管理", Controller = "Brand" },
        new() { Code = "system_model", Name = "型號管理", Controller = "PrinterModel" },
        new() { Code = "system_user", Name = "人員管理", Controller = "AppUser" },
        new() { Code = "system_module", Name = "模組管理", Controller = "SystemModule" },
        new() { Code = "system_permission", Name = "權限設定", Controller = "Permission" },
    };

    public PermissionService(PrinterDbContext context)
    {
        _context = context;
    }

    public List<FeatureDefinition> GetAllFeatures() => Features;

    public async Task<Dictionary<string, bool>> GetRolePermissionsAsync(string role)
    {
        var perms = await _context.RolePermissions
            .Where(p => p.Role == role)
            .ToDictionaryAsync(p => p.FeatureCode, p => p.IsAllowed);

        // admin 預設全部允許
        var result = new Dictionary<string, bool>();
        foreach (var f in Features)
        {
            if (perms.TryGetValue(f.Code, out var allowed))
                result[f.Code] = allowed;
            else
                result[f.Code] = role == "admin";
        }
        return result;
    }

    public async Task SaveRolePermissionsAsync(string role, Dictionary<string, bool> permissions)
    {
        var existing = await _context.RolePermissions
            .Where(p => p.Role == role)
            .ToListAsync();

        foreach (var (code, allowed) in permissions)
        {
            var perm = existing.FirstOrDefault(p => p.FeatureCode == code);
            if (perm != null)
            {
                perm.IsAllowed = allowed;
            }
            else
            {
                _context.RolePermissions.Add(new RolePermission
                {
                    Role = role,
                    FeatureCode = code,
                    IsAllowed = allowed
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasPermissionAsync(string role, string featureCode)
    {
        // admin 預設全部允許
        if (role == "admin") return true;

        var perm = await _context.RolePermissions
            .FirstOrDefaultAsync(p => p.Role == role && p.FeatureCode == featureCode);

        return perm?.IsAllowed ?? false;
    }

    public async Task InitializeDefaultPermissionsAsync()
    {
        if (await _context.RolePermissions.AnyAsync()) return;

        var defaults = new List<RolePermission>();

        // admin: 全部允許
        foreach (var f in Features)
        {
            defaults.Add(new RolePermission { Role = "admin", FeatureCode = f.Code, IsAllowed = true });
        }

        // supervisor: 大部分允許，排除人員管理/模組管理/權限設定
        foreach (var f in Features)
        {
            var allowed = f.Code != "system_user" && f.Code != "system_module" && f.Code != "system_permission";
            defaults.Add(new RolePermission { Role = "supervisor", FeatureCode = f.Code, IsAllowed = allowed });
        }

        // employee: 基本功能
        var employeeAllowed = new HashSet<string> { "home", "partner", "printer", "print_record", "work_order" };
        foreach (var f in Features)
        {
            defaults.Add(new RolePermission { Role = "employee", FeatureCode = f.Code, IsAllowed = employeeAllowed.Contains(f.Code) });
        }

        _context.RolePermissions.AddRange(defaults);
        await _context.SaveChangesAsync();
    }
}

using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;

namespace printer.Services.Impl;

/// <summary>
/// 模組服務實作
/// </summary>
public class ModuleService : IModuleService
{
    private readonly PrinterDbContext _context;

    public ModuleService(PrinterDbContext context)
    {
        _context = context;
    }

    public async Task<List<SystemModule>> GetAllModulesAsync()
    {
        return await _context.SystemModules
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Name)
            .ToListAsync();
    }

    public async Task<List<SystemModule>> GetEnabledModulesAsync()
    {
        return await _context.SystemModules
            .Where(m => m.IsEnabled)
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Name)
            .ToListAsync();
    }

    public async Task<SystemModule?> GetModuleByIdAsync(int id)
    {
        return await _context.SystemModules.FindAsync(id);
    }

    public async Task<SystemModule?> GetModuleByCodeAsync(string code)
    {
        return await _context.SystemModules
            .FirstOrDefaultAsync(m => m.Code == code);
    }

    public async Task<bool> IsModuleEnabledAsync(string moduleCode)
    {
        var module = await _context.SystemModules
            .FirstOrDefaultAsync(m => m.Code == moduleCode);
        return module?.IsEnabled ?? false;
    }

    public async Task<bool> ToggleModuleAsync(int moduleId)
    {
        var module = await _context.SystemModules.FindAsync(moduleId);
        if (module == null) return false;

        var wasEnabled = module.IsEnabled;
        module.IsEnabled = !module.IsEnabled;
        module.UpdatedAt = DateTime.UtcNow;

        // 停用時還原相關設定為初始值
        if (wasEnabled)
        {
            await ResetModuleSettingsAsync(module.Code);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    private async Task ResetModuleSettingsAsync(string moduleCode)
    {
        switch (moduleCode)
        {
            case "billing":
                // 停用所有事務機計費設定
                var billingConfigs = await _context.PrinterBillingConfigs.ToListAsync();
                foreach (var cfg in billingConfigs)
                    cfg.IsEnabled = false;
                break;

            case "billingstyle":
                // 重置帳單列印樣式為預設
                var printSettings = await _context.InvoicePrintSettings.FirstOrDefaultAsync();
                if (printSettings != null)
                {
                    printSettings.TemplateCode = "classic";
                    printSettings.PrimaryColor = null;
                }
                break;

            case "einvoice":
                // 停用所有發票平台並清除 API 金鑰
                var platforms = await _context.EinvoicePlatforms.ToListAsync();
                foreach (var p in platforms)
                {
                    p.IsActive    = false;
                    p.MerchantId  = null;
                    p.ApiKey      = null;
                    p.ApiSecret   = null;
                    p.ExtraParams = null;
                    p.IsSandbox   = true;
                }
                break;
        }
    }

    public async Task<SystemModule> CreateModuleAsync(SystemModule module)
    {
        module.CreatedAt = DateTime.UtcNow;
        module.UpdatedAt = DateTime.UtcNow;
        _context.SystemModules.Add(module);
        await _context.SaveChangesAsync();
        return module;
    }

    public async Task<SystemModule> UpdateModuleAsync(SystemModule module)
    {
        module.UpdatedAt = DateTime.UtcNow;
        _context.SystemModules.Update(module);
        await _context.SaveChangesAsync();
        return module;
    }

    public async Task<bool> DeleteModuleAsync(int id)
    {
        var module = await _context.SystemModules.FindAsync(id);
        if (module == null) return false;

        _context.SystemModules.Remove(module);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task InitializeDefaultModulesAsync()
    {
        var defaultModules = new List<SystemModule>
        {
            new SystemModule
            {
                Code = "billing",
                Name = "計費系統",
                Description = "管理計費設定、生成帳單、查看計費報表",
                IsEnabled = false,
                SortOrder = 1,
                Icon = "bi-calculator",
                MenuController = "Invoice",
                MenuAction = "Index"
            },
            new SystemModule
            {
                Code = "einvoice",
                Name = "發票系統",
                Description = "管理電子發票開立與作廢",
                IsEnabled = false,
                SortOrder = 2,
                Icon = "bi-receipt",
                MenuController = "Einvoice",
                MenuAction = "Index"
            },
            new SystemModule
            {
                Code = "billingstyle",
                Name = "帳單風格",
                Description = "管理帳單列印樣式，提供 30 種視覺風格供選擇與切換",
                IsEnabled = false,
                SortOrder = 3,
                Icon = "bi-palette",
                MenuController = "InvoicePrintSettings",
                MenuAction = "Index"
            },
            new SystemModule
            {
                Code = "workorder",
                Name = "派工管理",
                Description = "派工單建立、指派事務機與人員、逾期追蹤",
                IsEnabled = true,
                SortOrder = 4,
                Icon = "bi-wrench",
                MenuController = "WorkOrder",
                MenuAction = "Index"
            }
        };

        foreach (var module in defaultModules)
        {
            if (!await _context.SystemModules.AnyAsync(m => m.Code == module.Code))
            {
                _context.SystemModules.Add(module);
            }
        }

        await _context.SaveChangesAsync();

        // 初始化發票平台
        var defaultPlatforms = new List<EinvoicePlatform>
        {
            new()
            {
                Code = "ezpay",
                Name = "ezPay 藍新",
                Description = "藍新金流電子發票",
                ApiUrl = "https://inv.ezpay.com.tw/Api/invoice_issue",
                SandboxUrl = "https://cinv.ezpay.com.tw/Api/invoice_issue"
            },
            new()
            {
                Code = "ecpay",
                Name = "綠界 ECPay",
                Description = "綠界科技電子發票",
                ApiUrl = "https://einvoice.ecpay.com.tw/B2CInvoice/Issue",
                SandboxUrl = "https://einvoice-stage.ecpay.com.tw/B2CInvoice/Issue"
            },
            new()
            {
                Code = "tradevan",
                Name = "關貿 Tradevan",
                Description = "關貿網路電子發票",
                ApiUrl = "https://www.tradevan.com.tw/einvoice/api",
                SandboxUrl = "https://test.tradevan.com.tw/einvoice/api"
            }
        };

        foreach (var platform in defaultPlatforms)
        {
            if (!await _context.EinvoicePlatforms.AnyAsync(p => p.Code == platform.Code))
            {
                _context.EinvoicePlatforms.Add(platform);
            }
        }

        await _context.SaveChangesAsync();
    }
}

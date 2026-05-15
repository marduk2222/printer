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
                Description = "管理計費設定、生成帳單、查看帳單報表",
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

        // 舊資料遷移：合併 ecpay_b2c / ecpay_b2b 到統一的 ecpay
        // 優先取 ecpay_b2c 的金鑰（B2C/B2B 共用同一組憑證），刪掉 ecpay_b2b 重複列
        var legacyB2C = await _context.EinvoicePlatforms.FirstOrDefaultAsync(p => p.Code == "ecpay_b2c");
        var legacyB2B = await _context.EinvoicePlatforms.FirstOrDefaultAsync(p => p.Code == "ecpay_b2b");
        var existingEcpay = await _context.EinvoicePlatforms.FirstOrDefaultAsync(p => p.Code == "ecpay");
        // 取 legacy 中第一個有金鑰的當來源
        var creds = (legacyB2C is { } b2c && !string.IsNullOrEmpty(b2c.MerchantId)) ? legacyB2C
                  : (legacyB2B is { } b2b && !string.IsNullOrEmpty(b2b.MerchantId)) ? legacyB2B
                  : null;
        if (existingEcpay == null && legacyB2C != null)
        {
            legacyB2C.Code = "ecpay";
            existingEcpay = legacyB2C;
            await _context.SaveChangesAsync();
        }
        else if (existingEcpay == null && legacyB2B != null)
        {
            legacyB2B.Code = "ecpay";
            existingEcpay = legacyB2B;
            legacyB2B = null;
            await _context.SaveChangesAsync();
        }
        // 若 ecpay 已存在但金鑰為空，從 legacy 補回
        if (existingEcpay != null && string.IsNullOrEmpty(existingEcpay.MerchantId) && creds != null)
        {
            existingEcpay.MerchantId = creds.MerchantId;
            existingEcpay.ApiKey = creds.ApiKey;
            existingEcpay.ApiSecret = creds.ApiSecret;
            existingEcpay.IsSandbox = creds.IsSandbox;
            await _context.SaveChangesAsync();
        }
        if (legacyB2C != null && legacyB2C.Code != "ecpay") _context.EinvoicePlatforms.Remove(legacyB2C);
        if (legacyB2B != null && legacyB2B.Code != "ecpay") _context.EinvoicePlatforms.Remove(legacyB2B);
        await _context.SaveChangesAsync();

        // 移除已棄用的 Tradevan 平台
        var legacyTradevan = await _context.EinvoicePlatforms.FirstOrDefaultAsync(p => p.Code == "tradevan");
        if (legacyTradevan != null)
        {
            _context.EinvoicePlatforms.Remove(legacyTradevan);
            await _context.SaveChangesAsync();
        }

        // 初始化發票平台
        var defaultPlatforms = new List<EinvoicePlatform>
        {
            new()
            {
                Code = "ezpay",
                Name = "藍新 ezPay",
                Description = "藍新金流電子發票（AES-256-CBC + 32-byte PKCS7 + 小寫 hex；MerchantID + HashKey(32) + HashIV(16)）",
                ApiUrl = "https://inv.ezpay.com.tw",
                SandboxUrl = "https://cinv.ezpay.com.tw"
            },
            new()
            {
                Code = "ecpay",
                Name = "綠界 ECPay",
                Description = "綠界科技電子發票；買方有統編自動走 B2B 交換模式，否則走 B2C；AES-128-CBC + URLEncode + Base64",
                ApiUrl = "https://einvoice.ecpay.com.tw",
                SandboxUrl = "https://einvoice-stage.ecpay.com.tw"
            },
            new()
            {
                Code = "gateweb",
                Name = "關網 Gateweb",
                Description = "關網資訊電子發票（Domestic API；認證：username/password 取得 id_token，呼叫帶 companyKey）",
                ApiUrl = "https://ss.gwis.com.tw",
                SandboxUrl = "https://sstest.gwis.com.tw"
            }
        };

        foreach (var platform in defaultPlatforms)
        {
            var existing = await _context.EinvoicePlatforms.FirstOrDefaultAsync(p => p.Code == platform.Code);
            if (existing == null)
            {
                _context.EinvoicePlatforms.Add(platform);
            }
            else
            {
                existing.Name = platform.Name;
                existing.Description = platform.Description;
                existing.ApiUrl = platform.ApiUrl;
                existing.SandboxUrl = platform.SandboxUrl;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }
}

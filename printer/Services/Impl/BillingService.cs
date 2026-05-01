using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;
using printer.Data.Enums;

namespace printer.Services.Impl;

/// <summary>
/// 計費服務實作
/// </summary>
public class BillingService : IBillingService
{
    private readonly PrinterDbContext _context;

    public BillingService(PrinterDbContext context)
    {
        _context = context;
    }

    #region 計費設定管理

    public async Task<PrinterBillingConfig?> GetConfigAsync(int printerId)
    {
        return await _context.PrinterBillingConfigs
            .Include(c => c.Printer)
            .Include(c => c.Tiers)
            .FirstOrDefaultAsync(c => c.PrinterId == printerId);
    }

    public async Task<List<PrinterBillingConfig>> GetAllConfigsAsync()
    {
        return await _context.PrinterBillingConfigs
            .Include(c => c.Printer)
                .ThenInclude(p => p!.Partner)
            .Include(c => c.Tiers)
            .OrderBy(c => c.Printer!.Partner!.Name)
            .ThenBy(c => c.Printer!.Name)
            .ToListAsync();
    }

    public async Task<PrinterBillingConfig> SaveConfigAsync(PrinterBillingConfig config)
    {
        var existing = await _context.PrinterBillingConfigs
            .Include(c => c.Tiers)
            .FirstOrDefaultAsync(c => c.PrinterId == config.PrinterId);

        if (existing != null)
        {
            // SetValues 會嘗試覆蓋 PK，須先對齊 Id 與 CreatedAt
            config.Id = existing.Id;
            config.CreatedAt = existing.CreatedAt;
            _context.Entry(existing).CurrentValues.SetValues(config);
            existing.UpdatedAt = DateTime.UtcNow;

            if (config.Tiers != null)
            {
                if (existing.Tiers != null)
                    _context.BillingTiers.RemoveRange(existing.Tiers);

                foreach (var tier in config.Tiers)
                {
                    tier.BillingConfigId = existing.Id;
                    _context.BillingTiers.Add(tier);
                }
            }
        }
        else
        {
            config.CreatedAt = DateTime.UtcNow;
            config.UpdatedAt = DateTime.UtcNow;
            _context.PrinterBillingConfigs.Add(config);
        }

        await _context.SaveChangesAsync();
        return config;
    }

    public async Task<bool> DeleteConfigAsync(int printerId)
    {
        var config = await _context.PrinterBillingConfigs
            .FirstOrDefaultAsync(c => c.PrinterId == printerId);
        if (config == null) return false;

        _context.PrinterBillingConfigs.Remove(config);
        await _context.SaveChangesAsync();
        return true;
    }

    #endregion

    #region 計費模板

    public async Task<List<BillingTemplate>> GetTemplatesAsync()
    {
        return await _context.BillingTemplates
            .OrderByDescending(t => t.IsActive)
            .ThenBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<BillingTemplate?> GetTemplateAsync(int id)
    {
        return await _context.BillingTemplates
            .Include(t => t.Tiers.OrderBy(tier => tier.TierOrder))
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<BillingTemplate> SaveTemplateAsync(BillingTemplate template)
    {
        if (template.Id == 0)
        {
            template.CreatedAt = DateTime.UtcNow;
            template.UpdatedAt = DateTime.UtcNow;
            _context.BillingTemplates.Add(template);
        }
        else
        {
            var existing = await _context.BillingTemplates
                .Include(t => t.Tiers)
                .FirstOrDefaultAsync(t => t.Id == template.Id);
            if (existing == null)
                throw new ArgumentException("找不到指定的模板");

            _context.Entry(existing).CurrentValues.SetValues(template);
            existing.UpdatedAt = DateTime.UtcNow;

            if (existing.Tiers != null)
                _context.BillingTiers.RemoveRange(existing.Tiers);

            if (template.Tiers != null)
            {
                foreach (var tier in template.Tiers)
                {
                    tier.BillingTemplateId = existing.Id;
                    tier.BillingConfigId = null;
                    _context.BillingTiers.Add(tier);
                }
            }
        }

        await _context.SaveChangesAsync();
        return template;
    }

    public async Task<bool> DeleteTemplateAsync(int id)
    {
        var template = await _context.BillingTemplates.FindAsync(id);
        if (template == null) return false;

        _context.BillingTemplates.Remove(template);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<PrinterBillingConfig> ApplyTemplateAsync(int printerId, int templateId)
    {
        var template = await _context.BillingTemplates
            .Include(t => t.Tiers)
            .FirstOrDefaultAsync(t => t.Id == templateId);
        if (template == null)
            throw new ArgumentException("找不到指定的模板");

        var config = await GetConfigAsync(printerId) ?? new PrinterBillingConfig { PrinterId = printerId };
        BillingCalculator.CopyProfile(template, config);
        var savedConfig = await SaveConfigAsync(config);

        // 複製模板的張數類型單價到事務機
        var templateSheetPrices = await _context.BillingTemplateSheetPrices
            .Where(p => p.TemplateId == templateId).ToListAsync();

        var templateSheetTiers = await _context.BillingTemplateSheetTiers
            .Where(t => t.TemplateId == templateId)
            .OrderBy(t => t.SheetTypeId).ThenBy(t => t.TierOrder)
            .ToListAsync();

        if (templateSheetPrices.Any())
        {
            var existingPrices = await _context.PrinterBillingSheetPrices
                .Where(p => p.PrinterId == printerId).ToListAsync();
            _context.PrinterBillingSheetPrices.RemoveRange(existingPrices);

            var existingTiers = await _context.PrinterBillingSheetTiers
                .Where(t => t.PrinterId == printerId).ToListAsync();
            _context.PrinterBillingSheetTiers.RemoveRange(existingTiers);

            foreach (var tp in templateSheetPrices)
            {
                _context.PrinterBillingSheetPrices.Add(new PrinterBillingSheetPrice
                {
                    PrinterId = printerId,
                    SheetTypeId = tp.SheetTypeId,
                    UnitPrice = tp.UnitPrice,
                    DiscountPercent = tp.DiscountPercent,
                    FreePages = tp.FreePages,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            int order = 1;
            int? lastStId = null;
            foreach (var tt in templateSheetTiers)
            {
                if (lastStId != tt.SheetTypeId) { order = 1; lastStId = tt.SheetTypeId; }
                _context.PrinterBillingSheetTiers.Add(new PrinterBillingSheetTier
                {
                    PrinterId = printerId,
                    SheetTypeId = tt.SheetTypeId,
                    TierOrder = order++,
                    FromPages = tt.FromPages,
                    ToPages = tt.ToPages,
                    Price = tt.Price
                });
            }

            await _context.SaveChangesAsync();
        }

        return savedConfig;
    }

    #endregion

    #region 費用計算

    public async Task<BillingCalculation> CalculateAsync(int printerId, DateOnly startDate, DateOnly endDate)
    {
        var config = await GetConfigAsync(printerId);
        if (config == null)
        {
            return new BillingCalculation
            {
                PrinterId = printerId,
                Breakdown = "未設定計費方式"
            };
        }

        var records = await _context.PrintRecords
            .Where(r => r.PrinterId == printerId && r.Date >= startDate && r.Date <= endDate)
            .ToListAsync();

        var periodMonths = BillingCalculator.CalcMonths(startDate, endDate);
        var printerName = config.Printer?.Name ?? "";

        // 優先使用新張數類型計費（若此事務機機型有設定 sheet types）
        var sheetPrices = await _context.PrinterBillingSheetPrices
            .Include(p => p.SheetType)
            .Where(p => p.PrinterId == printerId)
            .ToListAsync();

        if (sheetPrices.Any() && records.Any())
        {
            var recordIds = records.Select(r => r.Id).ToList();
            var recordValues = await _context.PrintRecordValues
                .Include(v => v.SheetType)
                .Where(v => recordIds.Contains(v.RecordId))
                .ToListAsync();

            // 加總每種類型的張數
            var typeTotals = recordValues
                .GroupBy(v => v.SheetTypeId)
                .ToDictionary(g => g.Key, g => g.Sum(v => v.Value));

            // 若無 PrintRecordValues（舊格式記錄），以 BlackSheets/ColorSheets/LargeSheets 名稱回退
            if (!typeTotals.Any())
            {
                var legacyBlack = records.Sum(r => r.BlackSheets);
                var legacyColor = records.Sum(r => r.ColorSheets);
                var legacyLarge = records.Sum(r => r.LargeSheets);

                foreach (var sp in sheetPrices)
                {
                    var n = sp.SheetType?.Name ?? "";
                    if (legacyBlack > 0 && n == "黑白")
                        typeTotals[sp.SheetTypeId] = legacyBlack;
                    else if (legacyColor > 0 && n == "彩色")
                        typeTotals[sp.SheetTypeId] = legacyColor;
                    else if (legacyLarge > 0 && n == "大張")
                        typeTotals[sp.SheetTypeId] = legacyLarge;
                }
            }

            // 計算每種類型的計費張數（套用誤印率後，扣除贈送張數，但先不折抵）
            // key = SheetTypeId, value = (rawPages, afterDiscount, freePagesLeft, billedPages)
            var sheetCalc = new Dictionary<int, (int Raw, int AfterDiscount, int FreePagesLeft, int Billed)>();
            foreach (var sp in sheetPrices)
            {
                var rawPages = typeTotals.GetValueOrDefault(sp.SheetTypeId, 0);
                var afterDiscount = sp.DiscountPercent > 0
                    ? (int)Math.Ceiling(rawPages * (1 - sp.DiscountPercent / 100m))
                    : rawPages;
                var billed = Math.Max(0, afterDiscount - sp.FreePages);
                var freePagesLeft = Math.Max(0, sp.FreePages - afterDiscount);
                sheetCalc[sp.SheetTypeId] = (rawPages, afterDiscount, freePagesLeft, billed);
            }

            // 套用 Weight + OffsetOrder 折抵：每類型剩餘贈送 × Weight = 等價池，按 OffsetOrder 排序折抵其他類型計費
            // 未填則視為 weight=0（該類型不參與互換）, offset_order=0
            var sheetMeta = sheetPrices.ToDictionary(
                sp => sp.SheetTypeId,
                sp => (Weight: sp.Weight ?? 0m, OffsetOrder: sp.OffsetOrder ?? 0));
            var offsetByType = ApplyWeightedOffset(sheetCalc, sheetMeta);

            // 載入此事務機的張數類型階梯
            var sheetTiersData = await _context.PrinterBillingSheetTiers
                .Where(t => t.PrinterId == printerId)
                .OrderBy(t => t.SheetTypeId).ThenBy(t => t.TierOrder)
                .ToListAsync();
            var tiersBySheetType = sheetTiersData
                .GroupBy(t => t.SheetTypeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 計算張數費並產生明細
            decimal sheetPageFee = 0;
            var breakdownLines = new List<string>();
            foreach (var sp in sheetPrices)
            {
                if (!sheetCalc.TryGetValue(sp.SheetTypeId, out var calc)) continue;
                if (calc.Raw <= 0) continue;

                var billedPages = calc.Billed;
                bool hasTiers = tiersBySheetType.TryGetValue(sp.SheetTypeId, out var tiers) && tiers!.Count > 0;
                decimal fee;
                string priceDetail;

                if (hasTiers)
                {
                    // 階梯式計費（非累進）：找到 billedPages 所在區間，整體套用該區間單價
                    var matched = tiers!.FirstOrDefault(t =>
                        billedPages >= t.FromPages &&
                        (!t.ToPages.HasValue || billedPages <= t.ToPages.Value))
                        ?? tiers.Last();

                    fee = billedPages * matched.Price;
                    var toStr = matched.ToPages.HasValue ? matched.ToPages.Value.ToString("N0") : "∞";
                    priceDetail = $"[階梯 {matched.FromPages:N0}~{toStr}] × ${matched.Price:N4} = ${fee:N0}";
                }
                else if (sp.UnitPrice > 0)
                {
                    // 固定單價計費
                    fee = billedPages * sp.UnitPrice;
                    priceDetail = $"× ${sp.UnitPrice:N4} = ${fee:N0}";
                }
                else
                {
                    continue; // 無單價且無階梯，跳過
                }

                sheetPageFee += fee;

                var detail = $"{sp.SheetType?.Name}: {calc.Raw:N0} 張";
                if (sp.DiscountPercent > 0)
                    detail += $" ×{(100 - sp.DiscountPercent):N0}%(誤印)→{calc.AfterDiscount:N0} 張";
                if (sp.FreePages > 0)
                    detail += $" -{sp.FreePages:N0}(贈送)";
                if (offsetByType.TryGetValue(sp.SheetTypeId, out var offsetN) && offsetN > 0)
                    detail += $" -{offsetN:N0}(互換折抵)";
                detail += $" ={billedPages:N0} 張 {priceDetail}";
                breakdownLines.Add(detail);
            }

            // 月租費沿用舊邏輯
            decimal monthlyFee = config.IsEnabled && config.MonthlyFee > 0 ? config.MonthlyFee : 0;
            if (monthlyFee > 0)
                breakdownLines.Insert(0, $"月租費: ${monthlyFee:N0}");

            var result = new BillingCalculation
            {
                PrinterId = printerId,
                PrinterName = printerName,
                MonthlyFee = monthlyFee,
                PageFee = sheetPageFee,
                TotalFee = monthlyFee + sheetPageFee,
                BillingType = monthlyFee > 0 && sheetPageFee > 0
                    ? Data.Enums.BillingType.Hybrid
                    : monthlyFee > 0
                        ? Data.Enums.BillingType.Monthly
                        : Data.Enums.BillingType.PerPage,
                Breakdown = string.Join("\n", breakdownLines),
                FirstRecordDate = records.Min(r => r.Date),
                LastRecordDate = records.Max(r => r.Date),
                RecordCount = records.Count
            };

            return result;
        }

        // 舊有計費邏輯（BlackSheets / ColorSheets / LargeSheets）
        var totalBlack = records.Sum(r => r.BlackSheets);
        var totalColor = records.Sum(r => r.ColorSheets);
        var totalLarge = records.Sum(r => r.LargeSheets);

        var legacyResult = BillingCalculator.Calculate(config, printerId, totalBlack, totalColor, totalLarge, periodMonths, printerName);

        if (records.Any())
        {
            legacyResult.FirstRecordDate = records.Min(r => r.Date);
            legacyResult.LastRecordDate = records.Max(r => r.Date);
            legacyResult.RecordCount = records.Count;
        }

        return legacyResult;
    }

    public async Task<List<BillingCalculation>> CalculatePartnerAsync(int partnerId, DateOnly startDate, DateOnly endDate)
    {
        // 只計算「該客戶旗下且未加入計費群組」的事務機（群組成員張數費走 CalculateGroupAsync）
        var printerIds = await _context.Printers
            .Where(p => p.PartnerId == partnerId && p.IsActive && p.BillingGroupId == null)
            .Select(p => p.Id)
            .ToListAsync();

        var results = new List<BillingCalculation>();
        foreach (var printerId in printerIds)
        {
            var calculation = await CalculateAsync(printerId, startDate, endDate);
            results.Add(calculation);
        }

        return results;
    }

    public async Task<GroupBillingCalculation> CalculateGroupAsync(int groupId, DateOnly startDate, DateOnly endDate)
    {
        var group = await _context.PrinterBillingGroups
            .Include(g => g.Members).ThenInclude(p => p.Partner)
            .Include(g => g.SheetPrices).ThenInclude(sp => sp.SheetType)
            .Include(g => g.SheetTiers)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
            return new GroupBillingCalculation { GroupId = groupId, Breakdown = "找不到群組" };

        var result = new GroupBillingCalculation
        {
            GroupId = group.Id,
            GroupName = group.Name,
            BillingPartnerId = group.BillingPartnerId
        };

        var memberIds = group.Members.Select(m => m.Id).ToList();
        var memberConfigs = await _context.PrinterBillingConfigs
            .Where(c => memberIds.Contains(c.PrinterId))
            .ToListAsync();

        foreach (var member in group.Members.OrderBy(m => m.Name))
        {
            var cfg = memberConfigs.FirstOrDefault(c => c.PrinterId == member.Id);
            var fee = (cfg?.IsEnabled == true && cfg.MonthlyFee > 0) ? cfg.MonthlyFee : 0;
            result.Members.Add(new GroupMemberMonthlyFee
            {
                PrinterId = member.Id,
                PrinterName = member.Name,
                PartnerName = member.Partner?.Name ?? "",
                MonthlyFee = fee
            });
        }

        // 2) 張數：合併群組成員的 PrintRecord
        if (!memberIds.Any() || !group.SheetPrices.Any())
            return result;

        var records = await _context.PrintRecords
            .Where(r => memberIds.Contains(r.PrinterId) && r.Date >= startDate && r.Date <= endDate)
            .ToListAsync();

        if (!records.Any())
            return result;

        result.FirstRecordDate = records.Min(r => r.Date);
        result.LastRecordDate = records.Max(r => r.Date);
        result.RecordCount = records.Count;

        var recordIds = records.Select(r => r.Id).ToList();
        var recordValues = await _context.PrintRecordValues
            .Where(v => recordIds.Contains(v.RecordId))
            .ToListAsync();

        // 合計每種張數類型張數
        var typeTotals = recordValues
            .GroupBy(v => v.SheetTypeId)
            .ToDictionary(g => g.Key, g => g.Sum(v => v.Value));

        // 每台 × 每張數類型的貢獻（用於 breakdown）
        var perMemberContrib = new Dictionary<int, Dictionary<int, int>>(); // sheetTypeId -> printerId -> pages
        if (recordValues.Any())
        {
            var recordToPrinter = records.ToDictionary(r => r.Id, r => r.PrinterId);
            foreach (var rv in recordValues)
            {
                if (!recordToPrinter.TryGetValue(rv.RecordId, out var pid)) continue;
                if (!perMemberContrib.TryGetValue(rv.SheetTypeId, out var dict))
                {
                    dict = new Dictionary<int, int>();
                    perMemberContrib[rv.SheetTypeId] = dict;
                }
                dict[pid] = dict.GetValueOrDefault(pid, 0) + rv.Value;
            }
        }
        // legacy fallback：若沒有 PrintRecordValues，依 BlackSheets/ColorSheets/LargeSheets + 類型名稱回退
        if (!typeTotals.Any())
        {
            var legacyByMember = records
                .GroupBy(r => r.PrinterId)
                .ToDictionary(g => g.Key, g => (
                    Black: g.Sum(r => r.BlackSheets),
                    Color: g.Sum(r => r.ColorSheets),
                    Large: g.Sum(r => r.LargeSheets)));

            foreach (var sp in group.SheetPrices)
            {
                var n = sp.SheetType?.Name ?? "";
                int total = 0;
                var contrib = new Dictionary<int, int>();
                foreach (var (pid, t) in legacyByMember)
                {
                    int v = n switch { "黑白" => t.Black, "彩色" => t.Color, "大張" => t.Large, _ => 0 };
                    if (v > 0) { total += v; contrib[pid] = v; }
                }
                if (total > 0)
                {
                    typeTotals[sp.SheetTypeId] = total;
                    perMemberContrib[sp.SheetTypeId] = contrib;
                }
            }
        }

        // 套群組層級誤印率 + 贈送
        var sheetCalc = new Dictionary<int, (int Raw, int AfterDiscount, int FreePagesLeft, int Billed)>();
        foreach (var sp in group.SheetPrices)
        {
            var rawPages = typeTotals.GetValueOrDefault(sp.SheetTypeId, 0);
            var afterDiscount = sp.DiscountPercent > 0
                ? (int)Math.Ceiling(rawPages * (1 - sp.DiscountPercent / 100m))
                : rawPages;
            var billed = Math.Max(0, afterDiscount - sp.FreePages);
            var freePagesLeft = Math.Max(0, sp.FreePages - afterDiscount);
            sheetCalc[sp.SheetTypeId] = (rawPages, afterDiscount, freePagesLeft, billed);
        }

        // 套用 Weight + OffsetOrder 折抵（未填視為 weight=0 — 該類型不參與互換）
        var sheetMeta = group.SheetPrices.ToDictionary(
            sp => sp.SheetTypeId,
            sp => (Weight: sp.Weight ?? 0m, OffsetOrder: sp.OffsetOrder ?? 0));
        var offsetByType = ApplyWeightedOffset(sheetCalc, sheetMeta);

        // 群組階梯
        var tiersBySheetType = group.SheetTiers
            .GroupBy(t => t.SheetTypeId)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.TierOrder).ToList());

        var memberNameById = result.Members.ToDictionary(m => m.PrinterId, m => m.PrinterName);

        decimal totalPageFee = 0;
        var lines = new List<string>();
        foreach (var sp in group.SheetPrices)
        {
            if (!sheetCalc.TryGetValue(sp.SheetTypeId, out var calc)) continue;
            if (calc.Raw <= 0) continue;

            var billedPages = calc.Billed;
            decimal fee;
            string priceDetail;

            if (tiersBySheetType.TryGetValue(sp.SheetTypeId, out var tiers) && tiers.Count > 0)
            {
                var matched = tiers.FirstOrDefault(t =>
                    billedPages >= t.FromPages &&
                    (!t.ToPages.HasValue || billedPages <= t.ToPages.Value)) ?? tiers.Last();
                fee = billedPages * matched.Price;
                var toStr = matched.ToPages.HasValue ? matched.ToPages.Value.ToString("N0") : "∞";
                priceDetail = $"[階梯 {matched.FromPages:N0}~{toStr}] × ${matched.Price:N4} = ${fee:N0}";
            }
            else if (sp.UnitPrice > 0)
            {
                fee = billedPages * sp.UnitPrice;
                priceDetail = $"× ${sp.UnitPrice:N4} = ${fee:N0}";
            }
            else
            {
                continue;
            }

            totalPageFee += fee;

            var detail = $"{sp.SheetType?.Name}: 合計 {calc.Raw:N0} 張";
            if (sp.DiscountPercent > 0)
                detail += $" ×{(100 - sp.DiscountPercent):N0}%(誤印)→{calc.AfterDiscount:N0} 張";
            if (sp.FreePages > 0)
                detail += $" -{sp.FreePages:N0}(贈送)";
            if (offsetByType.TryGetValue(sp.SheetTypeId, out var offsetN) && offsetN > 0)
                detail += $" -{offsetN:N0}(互換折抵)";
            detail += $" ={billedPages:N0} 張 {priceDetail}";
            lines.Add(detail);

            if (perMemberContrib.TryGetValue(sp.SheetTypeId, out var contrib) && contrib.Count > 0)
            {
                var parts = contrib
                    .OrderBy(kv => memberNameById.GetValueOrDefault(kv.Key, ""))
                    .Select(kv => $"{memberNameById.GetValueOrDefault(kv.Key, $"#{kv.Key}")}: {kv.Value:N0} 張");
                lines.Add($"  成員貢獻：{string.Join("、", parts)}");
            }
        }

        result.PageFee = totalPageFee;
        result.Breakdown = string.Join("\n", lines);
        return result;
    }

    #endregion

    #region 帳單管理

    public async Task<List<Invoice>> GetInvoicesAsync(int? partnerId = null, string? status = null)
    {
        var query = _context.Invoices
            .Include(i => i.Partner)
            .Include(i => i.Items)
            .AsQueryable();

        if (partnerId.HasValue)
            query = query.Where(i => i.PartnerId == partnerId.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(i => i.Status == status);

        return await query
            .OrderByDescending(i => i.BillingPeriod)
            .ThenBy(i => i.Partner!.Name)
            .ToListAsync();
    }

    public async Task<Invoice?> GetInvoiceAsync(int id)
    {
        return await _context.Invoices
            .Include(i => i.Partner)
            .Include(i => i.Items)
                .ThenInclude(item => item.Printer)
            .Include(i => i.Items)
                .ThenInclude(item => item.BillingGroup)
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<Invoice> GenerateInvoiceAsync(int partnerId, DateOnly startDate, DateOnly endDate)
    {
        var partner = await _context.Partners.FindAsync(partnerId);
        if (partner == null)
            throw new ArgumentException("找不到指定的客戶");

        var period = $"{startDate:yyyyMMdd}-{endDate:yyyyMMdd}";

        var hasOverlap = await _context.Invoices.AnyAsync(i =>
            i.PartnerId == partnerId &&
            i.Status != "cancelled" &&
            i.PeriodStart <= endDate &&
            i.PeriodEnd >= startDate);
        if (hasOverlap)
            throw new InvalidOperationException($"該客戶在 {startDate:yyyy/MM/dd} ~ {endDate:yyyy/MM/dd} 期間已有帳單");

        // 1) 該客戶旗下、未加入群組的事務機 → 獨立計費
        var standaloneCalculations = await CalculatePartnerAsync(partnerId, startDate, endDate);

        // 2) 該客戶為結帳客戶的計費群組
        var groupIds = await _context.PrinterBillingGroups
            .Where(g => g.BillingPartnerId == partnerId && g.IsActive)
            .Select(g => g.Id)
            .ToListAsync();

        var groupCalculations = new List<GroupBillingCalculation>();
        foreach (var gid in groupIds)
        {
            var gc = await CalculateGroupAsync(gid, startDate, endDate);
            groupCalculations.Add(gc);
        }

        var invoiceNumber = await GenerateInvoiceNumberAsync(startDate.ToString("yyyyMM"));

        var invoice = new Invoice
        {
            InvoiceNumber = invoiceNumber,
            PartnerId = partnerId,
            BillingPeriod = period,
            PeriodStart = startDate,
            PeriodEnd = endDate,
            Status = "draft",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // 獨立計費列：每台一筆（沿用既有格式）
        foreach (var calc in standaloneCalculations.Where(c => c.TotalFee > 0))
        {
            invoice.Items.Add(new InvoiceItem
            {
                PrinterId = calc.PrinterId,
                BillingType = calc.BillingType,
                Description = $"{calc.PrinterName} - {calc.BillingType.ToDisplayName()}",
                BlackPages = calc.BlackPages,
                ColorPages = calc.ColorPages,
                LargePages = calc.LargePages,
                MonthlyFee = calc.MonthlyFee,
                PageFee = calc.PageFee,
                Subtotal = calc.TotalFee,
                Breakdown = !string.IsNullOrEmpty(calc.Breakdown) ? calc.Breakdown : null
            });
        }

        // 群組計費列：每成員月租分別一筆 + 群組合併張數一筆
        foreach (var gc in groupCalculations)
        {
            // 成員月租
            foreach (var m in gc.Members.Where(m => m.MonthlyFee > 0))
            {
                var desc = !string.IsNullOrEmpty(m.PartnerName) && m.PartnerName != partner.Name
                    ? $"{m.PrinterName}（{m.PartnerName}） - 月租費 [群組: {gc.GroupName}]"
                    : $"{m.PrinterName} - 月租費 [群組: {gc.GroupName}]";

                invoice.Items.Add(new InvoiceItem
                {
                    PrinterId = m.PrinterId,
                    BillingGroupId = gc.GroupId,
                    BillingType = BillingType.Monthly,
                    Description = desc,
                    MonthlyFee = m.MonthlyFee,
                    PageFee = 0,
                    Subtotal = m.MonthlyFee
                });
            }

            // 合併張數列
            if (gc.PageFee > 0 || !string.IsNullOrEmpty(gc.Breakdown))
            {
                var memberNames = gc.Members.Select(m => m.PrinterName).ToList();
                var headerLine = $"合併計費群組 [{gc.GroupName}]｜成員：{string.Join("、", memberNames)}";
                var fullBreakdown = !string.IsNullOrEmpty(gc.Breakdown)
                    ? headerLine + "\n" + gc.Breakdown
                    : headerLine;

                invoice.Items.Add(new InvoiceItem
                {
                    PrinterId = null,
                    BillingGroupId = gc.GroupId,
                    IsGroupSummary = true,
                    BillingType = BillingType.PerPage,
                    Description = headerLine,
                    MonthlyFee = 0,
                    PageFee = gc.PageFee,
                    Subtotal = gc.PageFee,
                    Breakdown = fullBreakdown
                });
            }
        }

        invoice.TotalAmount = invoice.Items.Sum(i => i.Subtotal);
        invoice.TaxAmount = 0;
        invoice.GrandTotal = invoice.TotalAmount + invoice.TaxAmount;

        if (!invoice.Items.Any())
            throw new InvalidOperationException("此期間無計費資料可產生帳單");

        _context.Invoices.Add(invoice);

        // 更新獨立計費的作帳日期
        foreach (var calc in standaloneCalculations.Where(c => c.TotalFee > 0))
        {
            var config = await _context.PrinterBillingConfigs
                .FirstOrDefaultAsync(c => c.PrinterId == calc.PrinterId);
            if (config != null)
            {
                config.LastMonthlyBilledDate = endDate;
                config.LastPageBilledDate = endDate;
                config.UpdatedAt = DateTime.UtcNow;
            }
        }

        // 群組成員月租作帳日期 + 群組張數作帳日期
        foreach (var gc in groupCalculations)
        {
            foreach (var m in gc.Members)
            {
                var config = await _context.PrinterBillingConfigs
                    .FirstOrDefaultAsync(c => c.PrinterId == m.PrinterId);
                if (config != null)
                {
                    config.LastMonthlyBilledDate = endDate;
                    config.UpdatedAt = DateTime.UtcNow;
                }
            }

            var group = await _context.PrinterBillingGroups.FindAsync(gc.GroupId);
            if (group != null)
            {
                group.LastPageBilledDate = endDate;
                group.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

        return invoice;
    }

    public async Task<List<Invoice>> BatchGenerateInvoicesAsync(DateOnly startDate, DateOnly endDate)
    {
        // 結帳客戶 = 旗下有獨立計費印表機 OR 為群組結帳客戶
        var standalonePartnerIds = await _context.PrinterBillingConfigs
            .Include(c => c.Printer)
            .Where(c => c.IsEnabled && c.Printer != null
                     && c.Printer.PartnerId.HasValue
                     && c.Printer.BillingGroupId == null)
            .Select(c => c.Printer!.PartnerId!.Value)
            .Distinct()
            .ToListAsync();

        var groupPartnerIds = await _context.PrinterBillingGroups
            .Where(g => g.IsActive)
            .Select(g => g.BillingPartnerId)
            .Distinct()
            .ToListAsync();

        var partnerIds = standalonePartnerIds.Union(groupPartnerIds).Distinct().ToList();

        var invoices = new List<Invoice>();
        foreach (var partnerId in partnerIds)
        {
            try
            {
                var invoice = await GenerateInvoiceAsync(partnerId, startDate, endDate);
                invoices.Add(invoice);
            }
            catch { }
        }

        return invoices;
    }

    public async Task<bool> ConfirmInvoiceAsync(int id)
    {
        var invoice = await _context.Invoices.FindAsync(id);
        if (invoice == null || invoice.Status != "draft") return false;

        invoice.Status = "confirmed";
        invoice.ConfirmedAt = DateTime.UtcNow;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MarkAsPaidAsync(int id)
    {
        var invoice = await _context.Invoices.FindAsync(id);
        if (invoice == null || invoice.Status == "cancelled") return false;

        invoice.Status = "paid";
        invoice.PaidAt = DateTime.UtcNow;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CancelInvoiceAsync(int id)
    {
        var invoice = await _context.Invoices.FindAsync(id);
        if (invoice == null || invoice.Status == "paid") return false;

        invoice.Status = "cancelled";
        invoice.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    #endregion

    #region 計費群組

    public async Task<List<PrinterBillingGroup>> GetGroupsAsync()
    {
        return await _context.PrinterBillingGroups
            .Include(g => g.BillingPartner)
            .Include(g => g.Members)
            .OrderByDescending(g => g.IsActive)
            .ThenBy(g => g.BillingPartner!.Name)
            .ThenBy(g => g.Name)
            .ToListAsync();
    }

    public async Task<PrinterBillingGroup?> GetGroupAsync(int id)
    {
        return await _context.PrinterBillingGroups
            .Include(g => g.BillingPartner)
            .Include(g => g.Members).ThenInclude(p => p.Partner)
            .Include(g => g.SheetPrices).ThenInclude(sp => sp.SheetType)
            .Include(g => g.SheetTiers).ThenInclude(t => t.SheetType)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<PrinterBillingGroup> SaveGroupAsync(PrinterBillingGroup group)
    {
        if (group.Id == 0)
        {
            group.CreatedAt = DateTime.UtcNow;
            group.UpdatedAt = DateTime.UtcNow;
            _context.PrinterBillingGroups.Add(group);
        }
        else
        {
            var existing = await _context.PrinterBillingGroups.FindAsync(group.Id);
            if (existing == null)
                throw new ArgumentException("找不到計費群組");

            existing.Name = group.Name;
            existing.Description = group.Description;
            existing.BillingPartnerId = group.BillingPartnerId;
            existing.IsActive = group.IsActive;
            existing.PageFeeCycle = group.PageFeeCycle;
            existing.PageStartDate = group.PageStartDate;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return group;
    }

    public async Task<bool> DeleteGroupAsync(int id)
    {
        var group = await _context.PrinterBillingGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (group == null) return false;

        // 檢查是否有未取消的帳單已包含此群組
        var hasInvoice = await _context.InvoiceItems.AnyAsync(i =>
            i.BillingGroupId == id && i.Invoice != null && i.Invoice.Status != "cancelled");
        if (hasInvoice)
            throw new InvalidOperationException("此群組已被帳單引用，無法刪除（請先取消相關帳單）");

        // 解除成員綁定
        foreach (var m in group.Members)
            m.BillingGroupId = null;

        _context.PrinterBillingGroups.Remove(group);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetPrinterGroupAsync(int printerId, int? groupId)
    {
        var printer = await _context.Printers.FindAsync(printerId);
        if (printer == null) return false;

        if (groupId.HasValue)
        {
            var group = await _context.PrinterBillingGroups.FindAsync(groupId.Value);
            if (group == null)
                throw new ArgumentException("找不到計費群組");
        }

        printer.BillingGroupId = groupId;
        printer.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<PrinterBillingGroup> ApplyTemplateToGroupAsync(int groupId, int templateId)
    {
        var group = await _context.PrinterBillingGroups.FindAsync(groupId);
        if (group == null)
            throw new ArgumentException("找不到計費群組");

        var template = await _context.BillingTemplates.FindAsync(templateId);
        if (template == null)
            throw new ArgumentException("找不到計費模板");

        var templatePrices = await _context.BillingTemplateSheetPrices
            .Where(p => p.TemplateId == templateId).ToListAsync();
        var templateTiers = await _context.BillingTemplateSheetTiers
            .Where(t => t.TemplateId == templateId)
            .OrderBy(t => t.SheetTypeId).ThenBy(t => t.TierOrder)
            .ToListAsync();

        // 清除群組既有 SheetPrice / SheetTier
        var existingPrices = await _context.BillingGroupSheetPrices
            .Where(p => p.GroupId == groupId).ToListAsync();
        _context.BillingGroupSheetPrices.RemoveRange(existingPrices);

        var existingTiers = await _context.BillingGroupSheetTiers
            .Where(t => t.GroupId == groupId).ToListAsync();
        _context.BillingGroupSheetTiers.RemoveRange(existingTiers);

        // 複製模板的張數類型計費（月租欄位忽略，群組無月租）
        foreach (var tp in templatePrices)
        {
            _context.BillingGroupSheetPrices.Add(new BillingGroupSheetPrice
            {
                GroupId = groupId,
                SheetTypeId = tp.SheetTypeId,
                UnitPrice = tp.UnitPrice,
                DiscountPercent = tp.DiscountPercent,
                FreePages = tp.FreePages,
                UpdatedAt = DateTime.UtcNow
            });
        }

        int order = 1;
        int? lastStId = null;
        foreach (var tt in templateTiers)
        {
            if (lastStId != tt.SheetTypeId) { order = 1; lastStId = tt.SheetTypeId; }
            _context.BillingGroupSheetTiers.Add(new BillingGroupSheetTier
            {
                GroupId = groupId,
                SheetTypeId = tt.SheetTypeId,
                TierOrder = order++,
                FromPages = tt.FromPages,
                ToPages = tt.ToPages,
                Price = tt.Price
            });
        }

        // 帶入張數計費週期與起算日
        group.PageFeeCycle = template.PageFeeCycle;
        if (template.PageStartDate.HasValue)
            group.PageStartDate = template.PageStartDate;
        group.UpdatedAt = DateTime.UtcNow;

        // 把模板月租寫到群組所有成員的 PrinterBillingConfig
        var memberIds = await _context.Printers
            .Where(p => p.BillingGroupId == groupId)
            .Select(p => p.Id)
            .ToListAsync();

        foreach (var memberId in memberIds)
        {
            var cfg = await _context.PrinterBillingConfigs
                .FirstOrDefaultAsync(c => c.PrinterId == memberId);
            if (cfg == null)
            {
                cfg = new PrinterBillingConfig
                {
                    PrinterId = memberId,
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.PrinterBillingConfigs.Add(cfg);
            }
            cfg.MonthlyFee = template.MonthlyFee;
            cfg.MonthlyFeeCycle = template.MonthlyFeeCycle;
            if (template.MonthlyStartDate.HasValue)
                cfg.MonthlyStartDate = template.MonthlyStartDate;
            cfg.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return group;
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 套用「等價單位池折抵」：剩餘贈送 × Weight = 等價池，依 OffsetOrder 折抵其他類型計費（不足 1 張不計）
    /// 使用後 sheetCalc 的 Billed 與 FreePagesLeft 會被更新，並把折抵張數記到 outOffsetPages（key=SheetTypeId）
    /// </summary>
    private static Dictionary<int, int> ApplyWeightedOffset(
        Dictionary<int, (int Raw, int AfterDiscount, int FreePagesLeft, int Billed)> sheetCalc,
        Dictionary<int, (decimal Weight, int OffsetOrder)> sheetMeta)
    {
        var offsetByType = new Dictionary<int, int>();

        decimal pool = 0;
        foreach (var kv in sheetCalc)
        {
            if (sheetMeta.TryGetValue(kv.Key, out var meta) && meta.Weight > 0)
                pool += kv.Value.FreePagesLeft * meta.Weight;
        }
        if (pool <= 0) return offsetByType;

        // 依 OffsetOrder 升序處理計費類型（小者優先享用折抵）
        var orderedKeys = sheetCalc.Keys
            .OrderBy(k => sheetMeta.TryGetValue(k, out var m) ? m.OffsetOrder : int.MaxValue)
            .ThenBy(k => k)
            .ToList();

        foreach (var key in orderedKeys)
        {
            var calc = sheetCalc[key];
            if (calc.Billed <= 0) continue;
            if (!sheetMeta.TryGetValue(key, out var meta) || meta.Weight <= 0) continue;

            var billedEqu = calc.Billed * meta.Weight;
            var available = Math.Min(pool, billedEqu);
            var offsetPages = (int)Math.Floor(available / meta.Weight); // 不足 1 不計
            if (offsetPages <= 0) continue;

            sheetCalc[key] = (calc.Raw, calc.AfterDiscount, calc.FreePagesLeft, calc.Billed - offsetPages);
            pool -= offsetPages * meta.Weight;
            offsetByType[key] = offsetPages;
            if (pool <= 0) break;
        }

        return offsetByType;
    }

    private async Task<string> GenerateInvoiceNumberAsync(string period)
    {
        var prefix = $"INV-{period}-";
        var count = await _context.Invoices
            .CountAsync(i => i.InvoiceNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D3}";
    }

    #endregion
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;
using printer.Services;
using printer.Services.Impl;

namespace printer.Controllers;

public class BillingConfigController : Controller
{
    private readonly PrinterDbContext _context;
    private readonly IBillingService _billingService;

    public BillingConfigController(PrinterDbContext context, IBillingService billingService)
    {
        _context = context;
        _billingService = billingService;
    }

    public async Task<IActionResult> Index(int? partnerId)
    {
        var configs = await _billingService.GetAllConfigsAsync();

        if (partnerId.HasValue)
        {
            configs = configs.Where(c => c.Printer?.PartnerId == partnerId.Value).ToList();
        }

        ViewBag.Partners = new SelectList(
            await _context.Partners.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name", partnerId);
        ViewBag.PartnerId = partnerId;

        // 載入事務機所屬群組資訊（用於列表顯示）
        var printerIds = configs.Select(c => c.PrinterId).ToList();
        var printerGroups = await _context.Printers
            .Where(p => printerIds.Contains(p.Id) && p.BillingGroupId.HasValue)
            .Include(p => p.BillingGroup)
            .ToDictionaryAsync(p => p.Id, p => p.BillingGroup);
        ViewBag.PrinterGroups = printerGroups;

        // 尚未設定計費的事務機（提供醒目入口）
        var configuredIds = configs.Select(c => c.PrinterId).ToHashSet();
        var unconfiguredQuery = _context.Printers
            .Include(p => p.Partner)
            .Include(p => p.BillingGroup)
            .Where(p => p.IsActive && !configuredIds.Contains(p.Id));
        if (partnerId.HasValue)
            unconfiguredQuery = unconfiguredQuery.Where(p => p.PartnerId == partnerId.Value);

        ViewBag.UnconfiguredPrinters = await unconfiguredQuery
            .OrderBy(p => p.Partner!.Name).ThenBy(p => p.Name)
            .ToListAsync();

        return View(configs);
    }

    public async Task<IActionResult> Edit(int printerId)
    {
        var printer = await _context.Printers
            .Include(p => p.Partner)
            .Include(p => p.Model)
            .FirstOrDefaultAsync(p => p.Id == printerId);

        if (printer == null) return NotFound();

        var config = await _billingService.GetConfigAsync(printerId)
            ?? new PrinterBillingConfig { PrinterId = printerId };

        ViewBag.Printer = printer;
        ViewBag.Templates = new SelectList(
            await _billingService.GetTemplatesAsync(), "Id", "Name");

        // 若此事務機已加入計費群組，載入群組資訊供 view 顯示提示
        if (printer.BillingGroupId.HasValue)
        {
            ViewBag.PrinterGroup = await _context.PrinterBillingGroups
                .Include(g => g.BillingPartner)
                .FirstOrDefaultAsync(g => g.Id == printer.BillingGroupId.Value);
        }

        // 可選擇的群組清單（啟用中）
        ViewBag.AvailableGroups = await _context.PrinterBillingGroups
            .Where(g => g.IsActive)
            .Include(g => g.BillingPartner)
            .OrderBy(g => g.BillingPartner!.Name).ThenBy(g => g.Name)
            .ToListAsync();

        // 載入所有啟用的張數類型（供下拉選單）
        var allSheetTypes = await _context.SheetTypes
            .Where(st => st.IsActive)
            .OrderBy(st => st.SortOrder).ThenBy(st => st.Id)
            .ToListAsync();

        // 載入此事務機已設定的單價（含 SheetType 導航）；依 SortOrder 排序，無設定則沿用 SheetType.SortOrder
        var sheetPrices = await _context.PrinterBillingSheetPrices
            .Where(p => p.PrinterId == printerId)
            .Include(p => p.SheetType)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.SheetType!.SortOrder).ThenBy(p => p.SheetTypeId)
            .ToListAsync();

        var sheetTiers = await _context.PrinterBillingSheetTiers
            .Where(t => t.PrinterId == printerId)
            .OrderBy(t => t.SheetTypeId).ThenBy(t => t.TierOrder)
            .ToListAsync();

        // 供下拉選單用：序列化全部 SheetType（id/name）
        ViewBag.AllSheetTypesJson = System.Text.Json.JsonSerializer.Serialize(
            allSheetTypes.Select(st => new { id = st.Id, name = st.Name }));
        // 供初始列渲染：已設定的 prices（含 sheetType name + Weight/OffsetOrder 覆蓋值）
        ViewBag.SheetPricesJson = System.Text.Json.JsonSerializer.Serialize(
            sheetPrices.Select(p => new {
                sheetTypeId = p.SheetTypeId,
                sheetTypeName = p.SheetType?.Name ?? "",
                unitPrice = p.UnitPrice,
                discountPercent = p.DiscountPercent,
                freePages = p.FreePages,
                weight = p.Weight,
                offsetOrder = p.OffsetOrder
            }));
        ViewBag.SheetTiersJson = System.Text.Json.JsonSerializer.Serialize(
            sheetTiers.Select(t => new { sheetTypeId = t.SheetTypeId, fromPages = t.FromPages, toPages = t.ToPages, price = t.Price }));

        return View(config);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSheetPrices(int printerId, List<int> sheetTypeIds,
        List<decimal> unitPrices, List<decimal> discountPercents, List<int> freePagesList)
    {
        for (int i = 0; i < sheetTypeIds.Count; i++)
        {
            var existing = await _context.PrinterBillingSheetPrices
                .FirstOrDefaultAsync(p => p.PrinterId == printerId && p.SheetTypeId == sheetTypeIds[i]);

            var unitPrice = i < unitPrices.Count ? unitPrices[i] : 0;
            var discount = i < discountPercents.Count ? discountPercents[i] : 0;
            var freePages = i < freePagesList.Count ? freePagesList[i] : 0;

            if (existing != null)
            {
                existing.UnitPrice = unitPrice;
                existing.DiscountPercent = discount;
                existing.FreePages = freePages;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.PrinterBillingSheetPrices.Add(new PrinterBillingSheetPrice
                {
                    PrinterId = printerId,
                    SheetTypeId = sheetTypeIds[i],
                    UnitPrice = unitPrice,
                    DiscountPercent = discount,
                    FreePages = freePages,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }
        await _context.SaveChangesAsync();
        TempData["Success"] = "張數單價已儲存";
        return RedirectToAction(nameof(Edit), new { printerId });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(
        PrinterBillingConfig config,
        string? tiersJson,
        List<int>? sheetTypeIds,
        List<decimal>? unitPrices,
        List<decimal>? discountPercents,
        List<int>? freePagesList,
        List<string>? weights,
        List<string>? offsetOrders,
        string? sheetTiersJson)
    {
        if (!ModelState.IsValid)
        {
            var printer = await _context.Printers
                .Include(p => p.Partner)
                .FirstOrDefaultAsync(p => p.Id == config.PrinterId);
            ViewBag.Printer = printer;
            ViewBag.Templates = new SelectList(
                await _billingService.GetTemplatesAsync(), "Id", "Name");
            return View(config);
        }

        var tiers = BillingCalculator.ParseTiers(tiersJson);
        if (tiers.Any())
            config.Tiers = tiers;

        // 保留作帳日期（不可由前端修改）
        var existingConfig = await _billingService.GetConfigAsync(config.PrinterId);
        if (existingConfig != null)
        {
            config.LastMonthlyBilledDate = existingConfig.LastMonthlyBilledDate;
            config.LastPageBilledDate = existingConfig.LastPageBilledDate;
        }

        await _billingService.SaveConfigAsync(config);

        // 儲存張數類型單價
        if (sheetTypeIds != null && sheetTypeIds.Count > 0)
        {
            for (int i = 0; i < sheetTypeIds.Count; i++)
            {
                var stId = sheetTypeIds[i];
                var existing = await _context.PrinterBillingSheetPrices
                    .FirstOrDefaultAsync(p => p.PrinterId == config.PrinterId && p.SheetTypeId == stId);
                var unitPrice = i < (unitPrices?.Count ?? 0) ? unitPrices![i] : 0;
                var discount = i < (discountPercents?.Count ?? 0) ? discountPercents![i] : 0;
                var freePages = i < (freePagesList?.Count ?? 0) ? freePagesList![i] : 0;
                decimal? weight = null;
                int? offsetOrder = null;
                if (i < (weights?.Count ?? 0) && decimal.TryParse(weights![i], out var w)) weight = w;
                if (i < (offsetOrders?.Count ?? 0) && int.TryParse(offsetOrders![i], out var o)) offsetOrder = o;

                if (existing != null)
                {
                    existing.UnitPrice = unitPrice;
                    existing.DiscountPercent = discount;
                    existing.FreePages = freePages;
                    existing.Weight = weight;
                    existing.OffsetOrder = offsetOrder;
                    existing.SortOrder = i + 1;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.PrinterBillingSheetPrices.Add(new PrinterBillingSheetPrice
                    {
                        PrinterId = config.PrinterId,
                        SheetTypeId = stId,
                        UnitPrice = unitPrice,
                        DiscountPercent = discount,
                        FreePages = freePages,
                        Weight = weight,
                        OffsetOrder = offsetOrder,
                        SortOrder = i + 1
                    });
                }
            }
        }

        // 儲存張數類型階梯
        var existingSheetTiers = await _context.PrinterBillingSheetTiers
            .Where(t => t.PrinterId == config.PrinterId).ToListAsync();
        _context.PrinterBillingSheetTiers.RemoveRange(existingSheetTiers);

        if (!string.IsNullOrWhiteSpace(sheetTiersJson))
        {
            try
            {
                var jsonOpts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsedTiers = System.Text.Json.JsonSerializer.Deserialize<List<SheetTierDto>>(sheetTiersJson, jsonOpts);
                if (parsedTiers != null)
                {
                    int order = 1;
                    int? lastStId = null;
                    foreach (var t in parsedTiers.Where(t => t.FromPages > 0))
                    {
                        if (lastStId != t.SheetTypeId) { order = 1; lastStId = t.SheetTypeId; }
                        _context.PrinterBillingSheetTiers.Add(new PrinterBillingSheetTier
                        {
                            PrinterId = config.PrinterId,
                            SheetTypeId = t.SheetTypeId,
                            TierOrder = order++,
                            FromPages = t.FromPages,
                            ToPages = t.ToPages,
                            Price = t.Price
                        });
                    }
                }
            }
            catch { }
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "計費設定已儲存";
        return RedirectToAction(nameof(Index));
    }

    private record SheetTierDto(int SheetTypeId, int FromPages, int? ToPages, decimal Price);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetGroup(int printerId, int? groupId)
    {
        try
        {
            await _billingService.SetPrinterGroupAsync(printerId, groupId);
            TempData["Success"] = groupId.HasValue ? "已加入群組" : "已移出群組";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Edit), new { printerId });
    }

    [HttpPost]
    public async Task<IActionResult> ApplyTemplate(int printerId, int templateId)
    {
        try
        {
            await _billingService.ApplyTemplateAsync(printerId, templateId);
            TempData["Success"] = "模板已套用";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Edit), new { printerId });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int printerId)
    {
        await _billingService.DeleteConfigAsync(printerId);
        TempData["Success"] = "計費設定已刪除";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> SelectPrinter(int? partnerId)
    {
        var configuredPrinterIds = (await _context.PrinterBillingConfigs
            .Select(c => c.PrinterId)
            .ToListAsync()).ToHashSet();

        var query = _context.Printers
            .Include(p => p.Partner)
            .Include(p => p.BillingGroup)
            .Where(p => p.IsActive);

        if (partnerId.HasValue)
            query = query.Where(p => p.PartnerId == partnerId.Value);

        var printers = await query
            .OrderBy(p => p.Partner!.Name)
            .ThenBy(p => p.Name)
            .ToListAsync();

        ViewBag.ConfiguredPrinterIds = configuredPrinterIds;
        ViewBag.Partners = new SelectList(
            await _context.Partners.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name", partnerId);
        ViewBag.PartnerId = partnerId;
        return View(printers);
    }
}

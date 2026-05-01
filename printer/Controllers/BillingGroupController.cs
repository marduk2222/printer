using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;
using printer.Services;

namespace printer.Controllers;

public class BillingGroupController : Controller
{
    private readonly PrinterDbContext _context;
    private readonly IBillingService _billingService;

    public BillingGroupController(PrinterDbContext context, IBillingService billingService)
    {
        _context = context;
        _billingService = billingService;
    }

    public async Task<IActionResult> Index()
    {
        var groups = await _billingService.GetGroupsAsync();
        return View(groups);
    }

    public async Task<IActionResult> Create()
    {
        await PopulatePartnersAsync(null);
        return View(new PrinterBillingGroup { IsActive = true, PageFeeCycle = 1 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PrinterBillingGroup group)
    {
        if (string.IsNullOrWhiteSpace(group.Name))
            ModelState.AddModelError(nameof(group.Name), "名稱必填");
        if (group.BillingPartnerId <= 0)
            ModelState.AddModelError(nameof(group.BillingPartnerId), "請選擇結帳客戶");

        if (!ModelState.IsValid)
        {
            await PopulatePartnersAsync(group.BillingPartnerId);
            return View(group);
        }

        var saved = await _billingService.SaveGroupAsync(group);
        TempData["Success"] = $"群組「{saved.Name}」已建立，請繼續設定成員與張數計費";
        return RedirectToAction(nameof(Edit), new { id = saved.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var group = await _billingService.GetGroupAsync(id);
        if (group == null) return NotFound();

        await PopulatePartnersAsync(group.BillingPartnerId);

        ViewBag.Templates = new SelectList(
            await _billingService.GetTemplatesAsync(), "Id", "Name");

        // 可加入的事務機（同 BillingPartner 或跨 Partner，未屬於其他群組）
        var availablePrinters = await _context.Printers
            .Include(p => p.Partner)
            .Where(p => p.IsActive
                     && (p.BillingGroupId == null || p.BillingGroupId == id))
            .OrderBy(p => p.Partner!.Name).ThenBy(p => p.Name)
            .ToListAsync();
        ViewBag.AvailablePrinters = availablePrinters;

        // 張數類型 + 群組現有設定
        var allSheetTypes = await _context.SheetTypes
            .Where(st => st.IsActive)
            .OrderBy(st => st.SortOrder).ThenBy(st => st.Id)
            .ToListAsync();

        ViewBag.AllSheetTypesJson = System.Text.Json.JsonSerializer.Serialize(
            allSheetTypes.Select(st => new { id = st.Id, name = st.Name }));

        ViewBag.SheetPricesJson = System.Text.Json.JsonSerializer.Serialize(
            group.SheetPrices
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.SheetType?.SortOrder ?? 0)
                .ThenBy(p => p.SheetTypeId)
                .Select(p => new
                {
                    sheetTypeId = p.SheetTypeId,
                    sheetTypeName = p.SheetType?.Name ?? "",
                    unitPrice = p.UnitPrice,
                    discountPercent = p.DiscountPercent,
                    freePages = p.FreePages,
                    weight = p.Weight,
                    offsetOrder = p.OffsetOrder
                }));

        ViewBag.SheetTiersJson = System.Text.Json.JsonSerializer.Serialize(
            group.SheetTiers.Select(t => new
            {
                sheetTypeId = t.SheetTypeId,
                fromPages = t.FromPages,
                toPages = t.ToPages,
                price = t.Price
            }));

        // 成員依 BillingGroupSortOrder 排序，方便兩個表格 (成員月租明細 / 群組成員) 顯示一致順序
        group.Members = group.Members
            .OrderBy(m => m.BillingGroupSortOrder)
            .ThenBy(m => m.Name)
            .ToList();

        // 載入成員月租（從各成員 PrinterBillingConfig）
        var memberIds = group.Members.Select(m => m.Id).ToList();
        var configs = await _context.PrinterBillingConfigs
            .Where(c => memberIds.Contains(c.PrinterId))
            .ToListAsync();
        ViewBag.MemberConfigs = configs.ToDictionary(c => c.PrinterId, c => c.MonthlyFee);

        return View(group);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        PrinterBillingGroup group,
        List<int>? sheetTypeIds,
        List<decimal>? unitPrices,
        List<decimal>? discountPercents,
        List<int>? freePagesList,
        List<string>? weights,
        List<string>? offsetOrders,
        string? sheetTiersJson,
        List<int>? memberPrinterIds,
        List<decimal>? memberMonthlyFees)
    {
        if (string.IsNullOrWhiteSpace(group.Name))
            ModelState.AddModelError(nameof(group.Name), "名稱必填");
        if (group.BillingPartnerId <= 0)
            ModelState.AddModelError(nameof(group.BillingPartnerId), "請選擇結帳客戶");

        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Edit), new { id = group.Id });
        }

        var existing = await _context.PrinterBillingGroups.FindAsync(group.Id);
        if (existing == null) return NotFound();

        existing.Name = group.Name;
        existing.Description = group.Description;
        existing.BillingPartnerId = group.BillingPartnerId;
        existing.IsActive = group.IsActive;
        existing.AutoAdjustGiftPool = group.AutoAdjustGiftPool;
        existing.PageFeeCycle = group.PageFeeCycle;
        existing.PageStartDate = group.PageStartDate;
        existing.UpdatedAt = DateTime.UtcNow;

        // 儲存 SheetPrice
        if (sheetTypeIds != null)
        {
            for (int i = 0; i < sheetTypeIds.Count; i++)
            {
                var stId = sheetTypeIds[i];
                var unit = i < (unitPrices?.Count ?? 0) ? unitPrices![i] : 0;
                var disc = i < (discountPercents?.Count ?? 0) ? discountPercents![i] : 0;
                var free = i < (freePagesList?.Count ?? 0) ? freePagesList![i] : 0;
                decimal? weight = null;
                int? offsetOrder = null;
                if (i < (weights?.Count ?? 0) && decimal.TryParse(weights![i], out var w)) weight = w;
                if (i < (offsetOrders?.Count ?? 0) && int.TryParse(offsetOrders![i], out var o)) offsetOrder = o;

                var sp = await _context.BillingGroupSheetPrices
                    .FirstOrDefaultAsync(p => p.GroupId == group.Id && p.SheetTypeId == stId);
                if (sp != null)
                {
                    sp.UnitPrice = unit;
                    sp.DiscountPercent = disc;
                    sp.FreePages = free;
                    sp.Weight = weight;
                    sp.OffsetOrder = offsetOrder;
                    sp.SortOrder = i + 1;
                    sp.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.BillingGroupSheetPrices.Add(new BillingGroupSheetPrice
                    {
                        GroupId = group.Id,
                        SheetTypeId = stId,
                        UnitPrice = unit,
                        DiscountPercent = disc,
                        FreePages = free,
                        Weight = weight,
                        OffsetOrder = offsetOrder,
                        SortOrder = i + 1,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            // 移除被取消勾選的張數類型
            var keptIds = sheetTypeIds.ToHashSet();
            var stale = await _context.BillingGroupSheetPrices
                .Where(p => p.GroupId == group.Id && !keptIds.Contains(p.SheetTypeId))
                .ToListAsync();
            _context.BillingGroupSheetPrices.RemoveRange(stale);
        }
        else
        {
            // 全部清空
            var all = await _context.BillingGroupSheetPrices
                .Where(p => p.GroupId == group.Id).ToListAsync();
            _context.BillingGroupSheetPrices.RemoveRange(all);
        }

        // 儲存階梯：先清掉再依 JSON 重建
        var existingTiers = await _context.BillingGroupSheetTiers
            .Where(t => t.GroupId == group.Id).ToListAsync();
        _context.BillingGroupSheetTiers.RemoveRange(existingTiers);

        if (!string.IsNullOrWhiteSpace(sheetTiersJson))
        {
            try
            {
                var jsonOpts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsed = System.Text.Json.JsonSerializer.Deserialize<List<SheetTierDto>>(sheetTiersJson, jsonOpts);
                if (parsed != null)
                {
                    int order = 1;
                    int? lastStId = null;
                    foreach (var t in parsed.Where(t => t.FromPages > 0))
                    {
                        if (lastStId != t.SheetTypeId) { order = 1; lastStId = t.SheetTypeId; }
                        _context.BillingGroupSheetTiers.Add(new BillingGroupSheetTier
                        {
                            GroupId = group.Id,
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

        // 寫回成員月租到各自 PrinterBillingConfig
        if (memberPrinterIds != null)
        {
            for (int i = 0; i < memberPrinterIds.Count; i++)
            {
                var pid = memberPrinterIds[i];
                var fee = i < (memberMonthlyFees?.Count ?? 0) ? memberMonthlyFees![i] : 0;

                var cfg = await _context.PrinterBillingConfigs
                    .FirstOrDefaultAsync(c => c.PrinterId == pid);
                if (cfg == null)
                {
                    cfg = new PrinterBillingConfig
                    {
                        PrinterId = pid,
                        IsEnabled = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.PrinterBillingConfigs.Add(cfg);
                }
                cfg.MonthlyFee = fee;
                cfg.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "群組設定已儲存";
        return RedirectToAction(nameof(Edit), new { id = group.Id });
    }

    private record SheetTierDto(int SheetTypeId, int FromPages, int? ToPages, decimal Price);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(int id, int printerId)
    {
        var group = await _context.PrinterBillingGroups.FindAsync(id);
        if (group == null) return NotFound();

        var printer = await _context.Printers.FindAsync(printerId);
        if (printer == null)
        {
            TempData["Error"] = "找不到事務機";
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (printer.BillingGroupId.HasValue && printer.BillingGroupId != id)
        {
            TempData["Error"] = "此事務機已屬於其他群組";
            return RedirectToAction(nameof(Edit), new { id });
        }

        printer.BillingGroupId = id;
        printer.UpdatedAt = DateTime.UtcNow;

        // 群組旗標啟用時自動調整贈送張數共用池
        var adjusted = group.AutoAdjustGiftPool
            ? await AdjustGiftPoolAsync(group, printerId, addToPool: true)
            : 0;
        await _context.SaveChangesAsync();

        TempData["Success"] = adjusted > 0
            ? $"已將 {printer.Name} 加入群組，並將 {adjusted} 類別的贈送張數加入共用池"
            : $"已將 {printer.Name} 加入群組";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(int id, int printerId)
    {
        var group = await _context.PrinterBillingGroups.FindAsync(id);
        if (group == null) return NotFound();

        var printer = await _context.Printers.FindAsync(printerId);
        if (printer == null || printer.BillingGroupId != id)
        {
            TempData["Error"] = "事務機不屬於此群組";
            return RedirectToAction(nameof(Edit), new { id });
        }

        // 群組旗標啟用時自動從共用池扣抵
        var adjusted = group.AutoAdjustGiftPool
            ? await AdjustGiftPoolAsync(group, printerId, addToPool: false)
            : 0;

        printer.BillingGroupId = null;
        printer.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["Success"] = adjusted > 0
            ? $"已將 {printer.Name} 移出群組，並從共用池扣抵 {adjusted} 類別的贈送張數"
            : $"已將 {printer.Name} 移出群組";
        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>
    /// 群組成員 / 成員月租明細 拖曳排序：依傳入 printerIds 順序寫回 BillingGroupSortOrder
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReorderMembers(int id, [FromForm] int[] printerIds)
    {
        var printers = await _context.Printers
            .Where(p => p.BillingGroupId == id && printerIds.Contains(p.Id))
            .ToListAsync();
        var indexMap = printerIds.Select((pid, i) => new { pid, i }).ToDictionary(x => x.pid, x => x.i + 1);
        foreach (var p in printers)
        {
            if (indexMap.TryGetValue(p.Id, out var order))
            {
                p.BillingGroupSortOrder = order;
                p.UpdatedAt = DateTime.UtcNow;
            }
        }
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    /// <summary>
    /// 把該機台各類別的贈送張數加入或扣抵群組共用池（呼叫者負責決定是否要呼叫）。
    /// 回傳實際異動的類別數。扣抵時 clamp 至 0，避免負值。
    /// </summary>
    private async Task<int> AdjustGiftPoolAsync(PrinterBillingGroup group, int printerId, bool addToPool)
    {
        var printerPrices = await _context.PrinterBillingSheetPrices
            .Where(p => p.PrinterId == printerId && p.FreePages > 0)
            .ToListAsync();
        if (printerPrices.Count == 0) return 0;

        var changed = 0;
        foreach (var pp in printerPrices)
        {
            var sp = await _context.BillingGroupSheetPrices
                .FirstOrDefaultAsync(g => g.GroupId == group.Id && g.SheetTypeId == pp.SheetTypeId);

            if (sp == null)
            {
                if (!addToPool) continue;
                _context.BillingGroupSheetPrices.Add(new BillingGroupSheetPrice
                {
                    GroupId = group.Id,
                    SheetTypeId = pp.SheetTypeId,
                    FreePages = pp.FreePages,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                sp.FreePages = addToPool
                    ? sp.FreePages + pp.FreePages
                    : Math.Max(0, sp.FreePages - pp.FreePages);
                sp.UpdatedAt = DateTime.UtcNow;
            }
            changed++;
        }
        return changed;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyTemplate(int id, int templateId)
    {
        try
        {
            await _billingService.ApplyTemplateToGroupAsync(id, templateId);
            TempData["Success"] = "模板已套用到群組";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var ok = await _billingService.DeleteGroupAsync(id);
            TempData["Success"] = ok ? "群組已刪除" : "找不到群組";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Edit), new { id });
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulatePartnersAsync(int? selectedId)
    {
        var partners = await _context.Partners
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
        ViewBag.Partners = new SelectList(partners, "Id", "Name", selectedId);
    }
}

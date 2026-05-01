using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;
using printer.Services;
using printer.Services.Impl;

namespace printer.Controllers;

public class BillingTemplateController : Controller
{
    private readonly IBillingService _billingService;
    private readonly PrinterDbContext _context;

    public BillingTemplateController(IBillingService billingService, PrinterDbContext context)
    {
        _billingService = billingService;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var templates = await _billingService.GetTemplatesAsync();
        return View(templates);
    }

    public async Task<IActionResult> Create()
    {
        await LoadViewBagData(0);
        return View("Edit", new BillingTemplate());
    }

    public async Task<IActionResult> Edit(int id)
    {
        var template = await _billingService.GetTemplateAsync(id);
        if (template == null) return NotFound();
        await LoadViewBagData(id);
        return View(template);
    }

    private async Task LoadViewBagData(int templateId)
    {
        var allSheetTypes = await _context.SheetTypes
            .Where(st => st.IsActive)
            .OrderBy(st => st.SortOrder).ThenBy(st => st.Id)
            .ToListAsync();

        ViewBag.AllSheetTypesJson = System.Text.Json.JsonSerializer.Serialize(
            allSheetTypes.Select(st => new { id = st.Id, name = st.Name }));

        if (templateId > 0)
        {
            var sheetPrices = await _context.BillingTemplateSheetPrices
                .Where(p => p.TemplateId == templateId)
                .Include(p => p.SheetType)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.SheetType!.SortOrder).ThenBy(p => p.SheetTypeId)
                .ToListAsync();

            var sheetTiers = await _context.BillingTemplateSheetTiers
                .Where(t => t.TemplateId == templateId)
                .OrderBy(t => t.SheetTypeId).ThenBy(t => t.TierOrder)
                .ToListAsync();

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
                sheetTiers.Select(t => new {
                    sheetTypeId = t.SheetTypeId,
                    fromPages = t.FromPages,
                    toPages = t.ToPages,
                    price = t.Price
                }));
        }
        else
        {
            ViewBag.SheetPricesJson = "[]";
            ViewBag.SheetTiersJson = "[]";
        }
    }

    [HttpPost]
    public async Task<IActionResult> Save(
        BillingTemplate template,
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
            await LoadViewBagData(template.Id);
            return View("Edit", template);
        }

        bool isNew = template.Id == 0;

        try
        {
            await _billingService.SaveTemplateAsync(template);
            int savedId = template.Id;

            // 儲存張數類型單價（全部替換）
            var existingPrices = await _context.BillingTemplateSheetPrices
                .Where(p => p.TemplateId == savedId).ToListAsync();
            _context.BillingTemplateSheetPrices.RemoveRange(existingPrices);

            if (sheetTypeIds != null && sheetTypeIds.Count > 0)
            {
                for (int i = 0; i < sheetTypeIds.Count; i++)
                {
                    decimal? weight = null;
                    int? offsetOrder = null;
                    if (i < (weights?.Count ?? 0) && decimal.TryParse(weights![i], out var w)) weight = w;
                    if (i < (offsetOrders?.Count ?? 0) && int.TryParse(offsetOrders![i], out var o)) offsetOrder = o;

                    _context.BillingTemplateSheetPrices.Add(new BillingTemplateSheetPrice
                    {
                        TemplateId = savedId,
                        SheetTypeId = sheetTypeIds[i],
                        UnitPrice = i < (unitPrices?.Count ?? 0) ? unitPrices![i] : 0,
                        DiscountPercent = i < (discountPercents?.Count ?? 0) ? discountPercents![i] : 0,
                        FreePages = i < (freePagesList?.Count ?? 0) ? freePagesList![i] : 0,
                        Weight = weight,
                        OffsetOrder = offsetOrder,
                        SortOrder = i + 1,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            // 儲存張數類型階梯（全部替換）
            var existingTiers = await _context.BillingTemplateSheetTiers
                .Where(t => t.TemplateId == savedId).ToListAsync();
            _context.BillingTemplateSheetTiers.RemoveRange(existingTiers);

            if (!string.IsNullOrWhiteSpace(sheetTiersJson))
            {
                try
                {
                    var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<List<SheetTierDto>>(sheetTiersJson, opts);
                    if (parsed != null)
                    {
                        int order = 1;
                        int? lastStId = null;
                        foreach (var t in parsed.Where(t => t.FromPages > 0))
                        {
                            if (lastStId != t.SheetTypeId) { order = 1; lastStId = t.SheetTypeId; }
                            _context.BillingTemplateSheetTiers.Add(new BillingTemplateSheetTier
                            {
                                TemplateId = savedId,
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

            TempData["Success"] = isNew ? "模板已建立" : "模板已更新";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            await LoadViewBagData(template.Id);
            return View("Edit", template);
        }
    }

    private record SheetTierDto(int SheetTypeId, int FromPages, int? ToPages, decimal Price);

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (await _billingService.DeleteTemplateAsync(id))
        {
            TempData["Success"] = "模板已刪除";
        }
        else
        {
            TempData["Error"] = "無法刪除模板";
        }
        return RedirectToAction(nameof(Index));
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;

namespace printer.Controllers;

[Authorize]
public class EinvoiceController : Controller
{
    private readonly PrinterDbContext _context;

    public EinvoiceController(PrinterDbContext context)
    {
        _context = context;
    }

    #region 發票管理

    public async Task<IActionResult> Index(int? partnerId, string? status, DateOnly? startDate, DateOnly? endDate, int page = 1)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        startDate ??= new DateOnly(today.Year, today.Month, 1);
        endDate ??= today;

        var query = _context.Einvoices
            .Include(e => e.Partner)
            .Include(e => e.Platform)
            .Where(e => e.InvoiceDate >= startDate.Value && e.InvoiceDate <= endDate.Value);

        if (partnerId.HasValue)
            query = query.Where(e => e.PartnerId == partnerId.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(e => e.Status == status);

        var pageSize = 30;
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.InvoiceDate)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.PartnerId = partnerId;
        ViewBag.Status = status;
        ViewBag.StartDate = startDate;
        ViewBag.EndDate = endDate;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        ViewBag.Total = total;
        ViewBag.Partners = new SelectList(
            await _context.Partners.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name", partnerId);

        return View(items);
    }

    public async Task<IActionResult> Create()
    {
        await LoadCreateViewBag();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int partnerId, int? platformId, DateOnly invoiceDate,
        string? buyerTaxId, string? buyerName, string? buyerEmail,
        string? carrierType, string? carrierId, string? donationCode,
        string taxType, int taxRate, string? note,
        List<EinvoiceItemInput> items)
    {
        if (!items.Any(i => i.Subtotal > 0))
        {
            TempData["Error"] = "請至少新增一筆明細";
            await LoadCreateViewBag();
            return View();
        }

        var partner = await _context.Partners.FindAsync(partnerId);

        var einvoice = new Einvoice
        {
            InvoiceNumber = await GenerateInvoiceNumber(invoiceDate),
            InvoiceDate = invoiceDate,
            PlatformId = platformId,
            PartnerId = partnerId,
            BuyerTaxId = string.IsNullOrWhiteSpace(buyerTaxId)
                ? (string.IsNullOrWhiteSpace(partner?.InvoiceTaxId) ? partner?.Vat : partner!.InvoiceTaxId)
                : buyerTaxId,
            BuyerName = string.IsNullOrWhiteSpace(buyerName)
                ? (string.IsNullOrWhiteSpace(partner?.InvoiceTitle) ? partner?.Name ?? "" : partner!.InvoiceTitle!)
                : buyerName,
            BuyerEmail = string.IsNullOrWhiteSpace(buyerEmail)
                ? (string.IsNullOrWhiteSpace(partner?.InvoiceEmail) ? partner?.Email : partner!.InvoiceEmail)
                : buyerEmail,
            CarrierType = string.IsNullOrWhiteSpace(carrierType) ? partner?.InvoiceCarrierType : carrierType,
            CarrierId = string.IsNullOrWhiteSpace(carrierId) ? partner?.InvoiceCarrierId : carrierId,
            DonationCode = string.IsNullOrWhiteSpace(donationCode) ? partner?.InvoiceDonationCode : donationCode,
            TaxType = taxType,
            TaxRate = taxRate,
            Note = string.IsNullOrWhiteSpace(note) ? partner?.InvoiceNote : note,
            Status = "draft",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var item in items.Where(i => i.Subtotal > 0))
        {
            einvoice.Items.Add(new EinvoiceItem
            {
                Description = item.Description ?? "",
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Subtotal = item.Subtotal
            });
        }

        einvoice.Amount = einvoice.Items.Sum(i => i.Subtotal);
        if (taxType == "taxable")
        {
            einvoice.TaxAmount = Math.Round(einvoice.Amount * taxRate / 100m);
        }
        einvoice.TotalAmount = einvoice.Amount + einvoice.TaxAmount;

        _context.Einvoices.Add(einvoice);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"發票 {einvoice.InvoiceNumber} 已建立";
        return RedirectToAction(nameof(Details), new { id = einvoice.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var einvoice = await _context.Einvoices
            .Include(e => e.Partner)
            .Include(e => e.Platform)
            .Include(e => e.BillingInvoice)
            .Include(e => e.Items)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (einvoice == null) return NotFound();
        return View(einvoice);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var einvoice = await _context.Einvoices
            .Include(e => e.Partner)
            .Include(e => e.Platform)
            .Include(e => e.Items)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (einvoice == null) return NotFound();
        if (einvoice.Status != "draft")
        {
            TempData["Error"] = "只有草稿狀態的發票可以編輯";
            return RedirectToAction(nameof(Details), new { id });
        }

        await LoadCreateViewBag();
        return View(einvoice);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id, DateOnly invoiceDate,
        string? buyerTaxId, string buyerName, string? buyerEmail,
        string? carrierType, string? carrierId, string? donationCode,
        string taxType, int taxRate, string? note,
        List<EinvoiceItemInput> items)
    {
        var einvoice = await _context.Einvoices
            .Include(e => e.Items)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (einvoice == null) return NotFound();
        if (einvoice.Status != "draft")
        {
            TempData["Error"] = "只有草稿狀態的發票可以編輯";
            return RedirectToAction(nameof(Details), new { id });
        }

        // 更新基本資訊
        einvoice.InvoiceDate = invoiceDate;
        einvoice.BuyerTaxId = buyerTaxId;
        einvoice.BuyerName = buyerName;
        einvoice.BuyerEmail = buyerEmail;
        einvoice.CarrierType = carrierType;
        einvoice.CarrierId = carrierId;
        einvoice.DonationCode = donationCode;
        einvoice.TaxType = taxType;
        einvoice.TaxRate = taxRate;
        einvoice.Note = note;
        einvoice.UpdatedAt = DateTime.UtcNow;

        // 替換明細
        _context.EinvoiceItems.RemoveRange(einvoice.Items);
        foreach (var item in items.Where(i => i.Subtotal > 0))
        {
            einvoice.Items.Add(new EinvoiceItem
            {
                Description = item.Description ?? "",
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Subtotal = item.Subtotal
            });
        }

        einvoice.Amount = einvoice.Items.Sum(i => i.Subtotal);
        einvoice.TaxAmount = taxType == "taxable" ? Math.Round(einvoice.Amount * taxRate / 100m) : 0;
        einvoice.TotalAmount = einvoice.Amount + einvoice.TaxAmount;

        await _context.SaveChangesAsync();
        TempData["Success"] = "發票草稿已更新";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(int id)
    {
        var einvoice = await _context.Einvoices.FindAsync(id);
        if (einvoice == null) return NotFound();

        if (einvoice.Status != "draft")
        {
            TempData["Error"] = "只有草稿狀態的發票可以開立";
            return RedirectToAction(nameof(Details), new { id });
        }

        einvoice.Status = "issued";
        einvoice.IssuedAt = DateTime.UtcNow;
        einvoice.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["Success"] = "發票已開立";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(int id, string? voidReason)
    {
        var einvoice = await _context.Einvoices.FindAsync(id);
        if (einvoice == null) return NotFound();

        if (einvoice.Status == "void")
        {
            TempData["Error"] = "發票已作廢";
            return RedirectToAction(nameof(Details), new { id });
        }

        einvoice.Status = "void";
        einvoice.VoidReason = voidReason;
        einvoice.VoidAt = DateTime.UtcNow;
        einvoice.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["Success"] = "發票已作廢";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var einvoice = await _context.Einvoices.FindAsync(id);
        if (einvoice != null && einvoice.Status == "draft")
        {
            _context.Einvoices.Remove(einvoice);
            await _context.SaveChangesAsync();
            TempData["Success"] = "發票已刪除";
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// 從帳單開立發票
    /// </summary>
    public async Task<IActionResult> CreateFromBilling(int billingInvoiceId)
    {
        var billing = await _context.Invoices
            .Include(i => i.Partner)
            .Include(i => i.Items)
            .ThenInclude(item => item.Printer)
            .FirstOrDefaultAsync(i => i.Id == billingInvoiceId);

        if (billing == null) return NotFound();

        var activePlatform = await _context.EinvoicePlatforms.FirstOrDefaultAsync(p => p.IsActive);

        ViewBag.BillingInvoice = billing;
        ViewBag.ActivePlatform = activePlatform;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFromBilling(
        int billingInvoiceId, int? platformId, DateOnly invoiceDate,
        string? buyerTaxId, string buyerName,
        string taxType, int taxRate, string? note,
        List<EinvoiceItemInput> items)
    {
        if (!items.Any(i => i.Subtotal > 0))
        {
            TempData["Error"] = "請至少新增一筆明細";
            return RedirectToAction(nameof(CreateFromBilling), new { billingInvoiceId });
        }

        var einvoice = new Einvoice
        {
            InvoiceNumber = await GenerateInvoiceNumber(invoiceDate),
            InvoiceDate = invoiceDate,
            PlatformId = platformId,
            BillingInvoiceId = billingInvoiceId,
            PartnerId = (await _context.Invoices.FindAsync(billingInvoiceId))!.PartnerId,
            BuyerTaxId = buyerTaxId,
            BuyerName = buyerName,
            TaxType = taxType,
            TaxRate = taxRate,
            Note = note,
            Status = "draft",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var item in items.Where(i => i.Subtotal > 0))
        {
            einvoice.Items.Add(new EinvoiceItem
            {
                Description = item.Description ?? "",
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Subtotal = item.Subtotal
            });
        }

        einvoice.Amount = einvoice.Items.Sum(i => i.Subtotal);
        if (taxType == "taxable")
        {
            einvoice.TaxAmount = Math.Round(einvoice.Amount * taxRate / 100m);
        }
        einvoice.TotalAmount = einvoice.Amount + einvoice.TaxAmount;

        _context.Einvoices.Add(einvoice);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"發票 {einvoice.InvoiceNumber} 已建立";
        return RedirectToAction(nameof(Details), new { id = einvoice.Id });
    }

    #endregion

    #region 平台設定

    public async Task<IActionResult> Platforms()
    {
        var platforms = await _context.EinvoicePlatforms.OrderBy(p => p.Name).ToListAsync();
        return View(platforms);
    }

    public async Task<IActionResult> EditPlatform(int id)
    {
        var platform = await _context.EinvoicePlatforms.FindAsync(id);
        if (platform == null) return NotFound();
        return View(platform);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPlatform(int id, string? merchantId, string? apiKey, string? apiSecret, bool isSandbox)
    {
        var existing = await _context.EinvoicePlatforms.FindAsync(id);
        if (existing == null) return NotFound();

        existing.MerchantId = merchantId;
        existing.ApiKey = apiKey;
        existing.ApiSecret = apiSecret;
        existing.IsSandbox = isSandbox;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        TempData["Success"] = $"{existing.Name} 設定已儲存";
        return RedirectToAction(nameof(Platforms));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActivatePlatform(int id)
    {
        // 停用所有平台
        var allPlatforms = await _context.EinvoicePlatforms.ToListAsync();
        foreach (var p in allPlatforms)
        {
            p.IsActive = p.Id == id;
            p.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        var activated = allPlatforms.FirstOrDefault(p => p.Id == id);
        TempData["Success"] = $"已啟用 {activated?.Name}";
        return RedirectToAction(nameof(Platforms));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateAllPlatforms()
    {
        var allPlatforms = await _context.EinvoicePlatforms.ToListAsync();
        foreach (var p in allPlatforms)
        {
            p.IsActive = false;
            p.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
        TempData["Success"] = "已停用所有平台";
        return RedirectToAction(nameof(Platforms));
    }

    #endregion

    #region 欄位對應

    public static readonly List<(string Code, string Name)> StandardFields = new()
    {
        ("invoice_number", "發票號碼"),
        ("invoice_date", "發票日期"),
        ("buyer_tax_id", "買方統編"),
        ("buyer_name", "買方名稱"),
        ("seller_tax_id", "賣方統編"),
        ("seller_name", "賣方名稱"),
        ("amount", "未稅金額"),
        ("tax_amount", "稅額"),
        ("total_amount", "含稅總額"),
        ("tax_rate", "稅率"),
        ("tax_type", "課稅類別"),
        ("item_description", "品名"),
        ("item_quantity", "數量"),
        ("item_unit_price", "單價"),
        ("item_subtotal", "小計"),
    };

    public async Task<IActionResult> FieldMappings(int id)
    {
        var platform = await _context.EinvoicePlatforms
            .Include(p => p.FieldMappings)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (platform == null) return NotFound();

        ViewBag.StandardFields = StandardFields;
        return View(platform);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveFieldMappings(int platformId, List<FieldMappingInput> mappings)
    {
        var platform = await _context.EinvoicePlatforms
            .Include(p => p.FieldMappings)
            .FirstOrDefaultAsync(p => p.Id == platformId);

        if (platform == null) return NotFound();

        // 刪除舊的對應
        _context.EinvoiceFieldMappings.RemoveRange(platform.FieldMappings);

        // 新增有填值的對應
        int order = 1;
        foreach (var m in mappings.Where(m => !string.IsNullOrWhiteSpace(m.ApiParamName)))
        {
            _context.EinvoiceFieldMappings.Add(new EinvoiceFieldMapping
            {
                PlatformId = platformId,
                FieldCode = m.FieldCode,
                ApiParamName = m.ApiParamName!.Trim(),
                DefaultValue = m.DefaultValue,
                Format = m.Format,
                IsRequired = m.IsRequired,
                SortOrder = order++
            });
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = $"已儲存 {platform.Name} 的欄位對應";
        return RedirectToAction(nameof(FieldMappings), new { id = platformId });
    }

    #endregion

    #region 私有方法

    private async Task<string> GenerateInvoiceNumber(DateOnly date)
    {
        var prefix = $"EI-{date:yyyyMM}";
        var count = await _context.Einvoices
            .CountAsync(e => e.InvoiceNumber.StartsWith(prefix));
        return $"{prefix}-{(count + 1):D4}";
    }

    private async Task LoadCreateViewBag()
    {
        ViewBag.Partners = new SelectList(
            await _context.Partners.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name");
        ViewBag.Platforms = new SelectList(
            await _context.EinvoicePlatforms.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name");
    }

    #endregion
}

public class FieldMappingInput
{
    public string FieldCode { get; set; } = string.Empty;
    public string? ApiParamName { get; set; }
    public string? DefaultValue { get; set; }
    public string? Format { get; set; }
    public bool IsRequired { get; set; }
}

public class EinvoiceItemInput
{
    public string? Description { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}

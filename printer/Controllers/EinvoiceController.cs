using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;
using printer.Services.Invoicing;

namespace printer.Controllers;

[Authorize]
public class EinvoiceController : Controller
{
    private readonly PrinterDbContext _context;
    private readonly IEinvoiceProviderFactory _providerFactory;
    private readonly ILogger<EinvoiceController> _logger;

    public EinvoiceController(PrinterDbContext context, IEinvoiceProviderFactory providerFactory, ILogger<EinvoiceController> logger)
    {
        _context = context;
        _providerFactory = providerFactory;
        _logger = logger;
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

        // 卡片統計：依目前的客戶 / 日期篩選範圍計算各狀態筆數（不受 status 過濾影響）
        var statsScope = _context.Einvoices
            .Where(e => e.InvoiceDate >= startDate.Value && e.InvoiceDate <= endDate.Value);
        if (partnerId.HasValue)
            statsScope = statsScope.Where(e => e.PartnerId == partnerId.Value);

        ViewBag.CountIssued = await statsScope.CountAsync(e => e.Status == "issued");
        ViewBag.CountDraft  = await statsScope.CountAsync(e => e.Status == "draft");
        ViewBag.CountVoid   = await statsScope.CountAsync(e => e.Status == "void");

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

    /// <summary>
    /// 開立發票（單一入口）：
    /// 不帶 billingInvoiceId → 手動模式；帶 billingInvoiceId → 帳單模式（預填帳單資料、鎖定客戶、自動帶入明細）。
    /// </summary>
    public async Task<IActionResult> Create(int? billingInvoiceId = null)
    {
        if (billingInvoiceId.HasValue)
        {
            var billing = await _context.Invoices
                .Include(i => i.Partner)
                .Include(i => i.Items).ThenInclude(it => it.Printer)
                .FirstOrDefaultAsync(i => i.Id == billingInvoiceId.Value);
            if (billing == null) return NotFound();
            ViewBag.BillingInvoice = billing;
        }
        ViewBag.ActivePlatform = await _context.EinvoicePlatforms.FirstOrDefaultAsync(p => p.IsActive);
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
        List<EinvoiceItemInput> items,
        int? billingInvoiceId = null,
        string invoiceType = "B2C",
        bool taxIncluded = false,
        string? action = null)
    {
        IActionResult ReturnToForm()
        {
            if (billingInvoiceId.HasValue)
                return RedirectToAction(nameof(Create), new { billingInvoiceId });
            return RedirectToAction(nameof(Create));
        }

        if (!items.Any(i => i.Subtotal > 0))
        {
            TempData["Error"] = "請至少新增一筆明細";
            return ReturnToForm();
        }

        if (invoiceType == "B2B" && string.IsNullOrWhiteSpace(buyerTaxId))
        {
            TempData["Error"] = "B2B 發票必須填寫買方統編";
            return ReturnToForm();
        }

        (IEinvoiceProvider Provider, EinvoicePlatform Platform)? active = null;
        if (action == "issue")
        {
            active = await _providerFactory.GetActiveAsync();
            if (active == null)
            {
                TempData["Error"] = "尚未啟用任何發票平台，無法立即開立。請先到「發票平台設定」啟用一家。";
                return ReturnToForm();
            }
        }

        var partner = await _context.Partners.FindAsync(partnerId);

        var einvoice = new Einvoice
        {
            InvoiceNumber = await GenerateInvoiceNumber(invoiceDate),
            InvoiceDate = invoiceDate,
            PlatformId = active?.Platform.Id ?? platformId,
            BillingInvoiceId = billingInvoiceId,
            PartnerId = partnerId,
            BuyerTaxId = invoiceType == "B2B"
                ? buyerTaxId
                : (string.IsNullOrWhiteSpace(buyerTaxId) ? null : buyerTaxId),
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
            InvoiceType = invoiceType,
            TaxIncluded = taxIncluded,
            Note = string.IsNullOrWhiteSpace(note) ? partner?.InvoiceNote : note,
            Status = "draft",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // 稅內含：明細為含稅 → 拆成未稅儲存（domain 統一為未稅）
        var divisor = taxIncluded && taxType == "taxable" ? 1m + taxRate / 100m : 1m;
        foreach (var item in items.Where(i => i.Subtotal > 0))
        {
            // 單價統一存整數（無分位），避免 0.95 之類的尾數
            var unitPriceUntaxed = Math.Round(item.UnitPrice / divisor, 0);
            var subtotalUntaxed = unitPriceUntaxed * item.Quantity;
            einvoice.Items.Add(new EinvoiceItem
            {
                Description = item.Description ?? "",
                Quantity = item.Quantity,
                UnitPrice = unitPriceUntaxed,
                Subtotal = subtotalUntaxed
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

        if (action == "issue" && active != null)
        {
            einvoice.Partner = partner;
            try
            {
                var result = await active.Value.Provider.IssueAsync(einvoice, active.Value.Platform);
                if (result.Success)
                {
                    if (!string.IsNullOrEmpty(result.Number))
                        einvoice.InvoiceNumber = result.Number;
                    if (result.IssueDate.HasValue)
                        einvoice.InvoiceDate = result.IssueDate.Value;
                    if (!string.IsNullOrEmpty(result.ProviderRelateNumber))
                        einvoice.ProviderRelateNumber = result.ProviderRelateNumber;
                    einvoice.Status = "issued";
                    einvoice.IssuedAt = DateTime.UtcNow;
                    einvoice.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"發票已開立 [{einvoice.InvoiceNumber}]（{active.Value.Platform.Name}）";
                }
                else
                {
                    _logger.LogWarning("Create issue failed [{Code}] {Msg}", result.Code, result.Message);
                    TempData["Error"] = $"草稿已建立但平台開立失敗：{result.Message}。可至發票詳情重試。";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "建立並立即開立例外 id={Id}", einvoice.Id);
                TempData["Error"] = $"草稿已建立但平台開立發生例外：{ex.Message}。可至發票詳情重試。";
            }
        }
        else
        {
            TempData["Success"] = $"發票草稿 {einvoice.InvoiceNumber} 已建立";
        }

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

        ViewBag.Allowances = await _context.EinvoiceAllowances
            .Where(a => a.EinvoiceId == id)
            .OrderByDescending(a => a.Id)
            .ToListAsync();

        // 上一筆 / 下一筆（發票間切換，依 Id 由大到小）
        ViewBag.NavPrev = await _context.Einvoices.Where(e => e.Id > id).OrderBy(e => e.Id)
            .Select(e => new { e.Id, Name = e.InvoiceNumber }).FirstOrDefaultAsync();
        ViewBag.NavNext = await _context.Einvoices.Where(e => e.Id < id).OrderByDescending(e => e.Id)
            .Select(e => new { e.Id, Name = e.InvoiceNumber }).FirstOrDefaultAsync();
        ViewBag.NavPosition = await _context.Einvoices.CountAsync(e => e.Id > id) + 1;
        ViewBag.NavTotal = await _context.Einvoices.CountAsync();

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
        List<EinvoiceItemInput> items,
        string invoiceType = "B2C",
        bool taxIncluded = false)
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

        if (invoiceType == "B2B" && string.IsNullOrWhiteSpace(buyerTaxId))
        {
            TempData["Error"] = "B2B 發票必須填寫買方統編";
            return RedirectToAction(nameof(Edit), new { id });
        }

        // 更新基本資訊
        einvoice.InvoiceDate = invoiceDate;
        einvoice.BuyerTaxId = invoiceType == "B2B" ? buyerTaxId : (string.IsNullOrWhiteSpace(buyerTaxId) ? null : buyerTaxId);
        einvoice.BuyerName = buyerName;
        einvoice.BuyerEmail = buyerEmail;
        einvoice.CarrierType = carrierType;
        einvoice.CarrierId = carrierId;
        einvoice.DonationCode = donationCode;
        einvoice.TaxType = taxType;
        einvoice.TaxRate = taxRate;
        einvoice.InvoiceType = invoiceType;
        einvoice.TaxIncluded = taxIncluded;
        einvoice.Note = note;
        einvoice.UpdatedAt = DateTime.UtcNow;

        // 替換明細：稅內含時把輸入（含稅）換算成未稅再存
        // 注意：必須先從 einvoice.Items navigation 清掉舊項目，再 Add 新項目，
        // 否則 sum 會同時加到「待刪除舊項」+「新項」造成金額翻倍
        var divisor = taxIncluded && taxType == "taxable" ? 1m + taxRate / 100m : 1m;
        var oldItems = einvoice.Items.ToList();
        einvoice.Items.Clear();
        _context.EinvoiceItems.RemoveRange(oldItems);
        foreach (var item in items.Where(i => i.Subtotal > 0))
        {
            // 單價統一存整數（無分位），避免 0.95 之類的尾數
            var unitPriceUntaxed = Math.Round(item.UnitPrice / divisor, 0);
            var subtotalUntaxed = unitPriceUntaxed * item.Quantity;
            einvoice.Items.Add(new EinvoiceItem
            {
                Description = item.Description ?? "",
                Quantity = item.Quantity,
                UnitPrice = unitPriceUntaxed,
                Subtotal = subtotalUntaxed
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
        var einvoice = await _context.Einvoices
            .Include(e => e.Items)
            .Include(e => e.Platform)
            .Include(e => e.Partner)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (einvoice == null) return NotFound();

        if (einvoice.Status != "draft")
        {
            TempData["Error"] = "只有草稿狀態的發票可以開立";
            return RedirectToAction(nameof(Details), new { id });
        }

        var active = await _providerFactory.GetActiveAsync();
        if (active == null)
        {
            TempData["Error"] = "未啟用任何發票平台，請先到平台設定啟用一家。";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            var result = await active.Value.Provider.IssueAsync(einvoice, active.Value.Platform);
            if (!result.Success)
            {
                _logger.LogWarning("Issue failed [{Code}] {Msg}", result.Code, result.Message);
                TempData["Error"] = $"開立失敗：{result.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!string.IsNullOrEmpty(result.Number))
                einvoice.InvoiceNumber = result.Number;
            if (result.IssueDate.HasValue)
                einvoice.InvoiceDate = result.IssueDate.Value;
            if (!string.IsNullOrEmpty(result.ProviderRelateNumber))
                einvoice.ProviderRelateNumber = result.ProviderRelateNumber;
            einvoice.PlatformId = active.Value.Platform.Id;
            einvoice.Status = "issued";
            einvoice.IssuedAt = DateTime.UtcNow;
            einvoice.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"發票已開立 [{einvoice.InvoiceNumber}]";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "開立發票例外 id={Id}", id);
            TempData["Error"] = $"開立發票時發生例外：{ex.Message}";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(int id, string? voidReason)
    {
        var einvoice = await _context.Einvoices
            .Include(e => e.Platform)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (einvoice == null) return NotFound();

        if (einvoice.Status == "void")
        {
            TempData["Error"] = "發票已作廢";
            return RedirectToAction(nameof(Details), new { id });
        }

        // 已開立 → 呼叫平台作廢；草稿 → 直接標記作廢（未上傳平台）
        if (einvoice.Status == "issued")
        {
            var active = await _providerFactory.GetActiveAsync();
            if (active == null)
            {
                TempData["Error"] = "未啟用任何發票平台，無法作廢已開立發票。";
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                var result = await active.Value.Provider.InvalidAsync(einvoice, voidReason ?? "客戶取消", active.Value.Platform);
                if (!result.Success)
                {
                    TempData["Error"] = $"作廢失敗：{result.Message}";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "作廢發票例外 id={Id}", id);
                TempData["Error"] = $"作廢發票時發生例外：{ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
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
    /// 舊路徑相容：/Einvoice/CreateFromBilling/{billingInvoiceId} → /Einvoice/Create?billingInvoiceId=...
    /// </summary>
    public IActionResult CreateFromBilling(int billingInvoiceId)
        => RedirectToActionPermanent(nameof(Create), new { billingInvoiceId });

    #endregion

    #region 折讓

    public async Task<IActionResult> CreateAllowance(int einvoiceId)
    {
        var einvoice = await _context.Einvoices
            .Include(e => e.Items)
            .Include(e => e.Partner)
            .FirstOrDefaultAsync(e => e.Id == einvoiceId);
        if (einvoice == null) return NotFound();
        if (einvoice.Status != "issued")
        {
            TempData["Error"] = "只有已開立的發票可以開立折讓";
            return RedirectToAction(nameof(Details), new { id = einvoiceId });
        }

        ViewBag.Einvoice = einvoice;
        var allowance = new EinvoiceAllowance
        {
            EinvoiceId = einvoice.Id,
            AllowanceDate = DateOnly.FromDateTime(DateTime.Today),
            PlatformId = einvoice.PlatformId
        };
        return View(allowance);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAllowance(int einvoiceId, string allowanceType, decimal amount, decimal taxAmount, string? note, List<EinvoiceItemInput>? items)
    {
        var einvoice = await _context.Einvoices
            .Include(e => e.Platform)
            .FirstOrDefaultAsync(e => e.Id == einvoiceId);
        if (einvoice == null) return NotFound();
        if (einvoice.Status != "issued")
        {
            TempData["Error"] = "只有已開立的發票可以開立折讓";
            return RedirectToAction(nameof(Details), new { id = einvoiceId });
        }

        var allowance = new EinvoiceAllowance
        {
            EinvoiceId = einvoice.Id,
            PlatformId = einvoice.PlatformId,
            AllowanceDate = DateOnly.FromDateTime(DateTime.Today),
            AllowanceType = allowanceType ?? "paper",
            Amount = amount,
            TaxAmount = taxAmount,
            TotalAmount = amount + taxAmount,
            Note = note,
            Status = "draft"
        };

        // 折讓繼承原發票稅率規則：稅內含時表單輸入為含稅，必須拆成未稅存進 DB
        // 否則 provider 把含稅再 × 稅率，會與 AllowanceAmount 不一致導致平台拒絕
        var divisor = einvoice.TaxIncluded && einvoice.TaxType == "taxable" ? 1m + einvoice.TaxRate / 100m : 1m;
        if (items != null)
        {
            foreach (var it in items.Where(x => !string.IsNullOrWhiteSpace(x.Description)))
            {
                var qty = it.Quantity > 0 ? it.Quantity : 1;
                var unitUntaxed = Math.Round(it.UnitPrice / divisor, 0);
                allowance.Items.Add(new EinvoiceAllowanceItem
                {
                    Description = it.Description!,
                    Quantity = qty,
                    UnitPrice = unitUntaxed,
                    Subtotal = unitUntaxed * qty
                });
            }
        }

        _context.EinvoiceAllowances.Add(allowance);
        await _context.SaveChangesAsync();

        TempData["Success"] = "折讓單已建立（草稿），請按開立送出";
        return RedirectToAction(nameof(AllowanceDetails), new { id = allowance.Id });
    }

    public async Task<IActionResult> AllowanceDetails(int id)
    {
        var allowance = await _context.EinvoiceAllowances
            .Include(a => a.Items)
            .Include(a => a.Einvoice).ThenInclude(e => e!.Partner)
            .Include(a => a.Platform)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (allowance == null) return NotFound();
        return View(allowance);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IssueAllowance(int id)
    {
        var allowance = await _context.EinvoiceAllowances
            .Include(a => a.Items)
            .Include(a => a.Einvoice)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (allowance?.Einvoice == null) return NotFound();
        if (allowance.Status != "draft")
        {
            TempData["Error"] = "只有草稿狀態的折讓單可以開立";
            return RedirectToAction(nameof(AllowanceDetails), new { id });
        }

        var active = await _providerFactory.GetActiveAsync();
        if (active == null)
        {
            TempData["Error"] = "未啟用任何發票平台。";
            return RedirectToAction(nameof(AllowanceDetails), new { id });
        }

        try
        {
            var result = await active.Value.Provider.AllowanceAsync(allowance, allowance.Einvoice, active.Value.Platform);
            if (!result.Success)
            {
                TempData["Error"] = $"開立折讓失敗：{result.Message}";
                return RedirectToAction(nameof(AllowanceDetails), new { id });
            }
            if (!string.IsNullOrEmpty(result.Number)) allowance.AllowanceNumber = result.Number;
            allowance.PlatformId = active.Value.Platform.Id;
            allowance.Status = "issued";
            allowance.IssuedAt = DateTime.UtcNow;
            allowance.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"折讓已開立 [{allowance.AllowanceNumber}]";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "開立折讓例外 id={Id}", id);
            TempData["Error"] = $"開立折讓時發生例外：{ex.Message}";
        }
        return RedirectToAction(nameof(AllowanceDetails), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VoidAllowance(int id, string? voidReason)
    {
        var allowance = await _context.EinvoiceAllowances
            .Include(a => a.Einvoice)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (allowance?.Einvoice == null) return NotFound();
        if (allowance.Status == "void")
        {
            TempData["Error"] = "折讓已作廢";
            return RedirectToAction(nameof(AllowanceDetails), new { id });
        }

        if (allowance.Status == "issued")
        {
            var active = await _providerFactory.GetActiveAsync();
            if (active == null)
            {
                TempData["Error"] = "未啟用任何發票平台。";
                return RedirectToAction(nameof(AllowanceDetails), new { id });
            }

            try
            {
                var result = await active.Value.Provider.AllowanceInvalidAsync(allowance, voidReason ?? "折讓取消", active.Value.Platform);
                if (!result.Success)
                {
                    TempData["Error"] = $"作廢折讓失敗：{result.Message}";
                    return RedirectToAction(nameof(AllowanceDetails), new { id });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "作廢折讓例外 id={Id}", id);
                TempData["Error"] = $"作廢折讓時發生例外：{ex.Message}";
                return RedirectToAction(nameof(AllowanceDetails), new { id });
            }
        }

        allowance.Status = "void";
        allowance.VoidReason = voidReason;
        allowance.VoidAt = DateTime.UtcNow;
        allowance.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["Success"] = "折讓已作廢";
        return RedirectToAction(nameof(AllowanceDetails), new { id });
    }

    /// <summary>
    /// 診斷：用 dummy 發票實際送 sandbox 並回 raw response。
    /// action: issue | invalid | allowance | allowance_invalid
    /// number: invalid/allowance/allowance_invalid 都需要原發票 InvoiceNo
    /// allowanceNumber: allowance_invalid 才用
    /// </summary>
    public async Task<IActionResult> Diagnose(string code, string op = "issue", string? number = null, string? allowanceNumber = null, bool b2bMode = false, string? orderNo = null)
    {
        var platform = await _context.EinvoicePlatforms.FirstOrDefaultAsync(p => p.Code == code);
        if (platform == null) return Content($"找不到平台 code={code}", "text/plain; charset=utf-8");

        if (op == "selftest" && code == "ezpay")
        {
            var sample = "MerchantID=310588623&Amt=100&TotalAmt=105&Version=1.5";
            var (cipher, rt, ok) = printer.Services.Invoicing.Providers.EzpayProvider.SelfTest(sample, platform.ApiKey ?? "", platform.ApiSecret ?? "");
            return Content(
                $"=== ezPay SelfTest ===\nplain: {sample}\ncipher hex({cipher.Length}): {cipher}\nround-trip: {rt}\nmatch: {ok}\n",
                "text/plain; charset=utf-8");
        }

        IEinvoiceProvider provider;
        try { provider = _providerFactory.Get(code); }
        catch (Exception ex) { return Content($"取得 provider 失敗：{ex.Message}", "text/plain; charset=utf-8"); }

        var isB2B = code == "ecpay_b2b" || b2bMode;
        // ezPay 等平台使用 ASCII 名稱排除 URL encoding 大小寫差異
        var asciiOnly = code == "ezpay";
        var buyerName = isB2B
            ? (asciiOnly ? "TSMC" : "台積電股份有限公司")
            : (asciiOnly ? "DiagBuyer" : "診斷用買方");
        var itemName = asciiOnly ? "TestItem" : "測試商品";

        var dummy = new Einvoice
        {
            Id = 99999,
            InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
            InvoiceNumber = number ?? string.Empty,
            BuyerName = buyerName,
            BuyerEmail = "diag@example.com",
            BuyerTaxId = isB2B ? "22099131" : null,
            // Gateweb sellerIdentifier 來源：ExtraParams 的 sellerDepartment 前綴
            SellerTaxId = code == "gateweb" ? GetGatewebSellerTaxId(platform) : "23639781",
            SellerName = asciiOnly ? "DiagSeller" : "診斷用賣方",
            CarrierType = null,
            TaxType = "taxable",
            TaxRate = 5,
            Amount = 100,
            TaxAmount = 5,
            TotalAmount = 105,
            // Gateweb invalid 用 Note 帶原發票 relateNumber (orderNo)
            Note = !string.IsNullOrEmpty(orderNo) ? $"ORDERNO={orderNo}" : "Diagnose smoke test",
            Partner = new Partner { Name = buyerName, Address = asciiOnly ? "Taipei City" : "台北市信義區信義路五段7號", Phone = "0223456789" },
            Items = new List<EinvoiceItem>
            {
                new() { Description = itemName, Quantity = 1, UnitPrice = 105, Subtotal = 105 }
            }
        };

        // 折讓 dummy：金額必須與 items 一致（含稅 105）
        var dummyAllowance = new EinvoiceAllowance
        {
            Id = 99999,
            EinvoiceId = 99999,
            AllowanceDate = DateOnly.FromDateTime(DateTime.Today),
            AllowanceType = "paper",
            AllowanceNumber = allowanceNumber,
            Einvoice = dummy,
            Amount = 100,
            TaxAmount = 5,
            TotalAmount = 105,
            // ezPay 折讓需要與原發票相同的 MerchantOrderNo，以 Note 標記透給 provider
            Note = !string.IsNullOrEmpty(orderNo) ? $"ORDERNO={orderNo}" : null,
            Items = new List<EinvoiceAllowanceItem>
            {
                new() { Description = itemName, Quantity = 1, UnitPrice = 105, Subtotal = 105 }
            }
        };

        try
        {
            EinvoiceProviderResult result;
            if (op == "b2b_register")
            {
                if (provider is not printer.Services.Invoicing.Providers.EcpayB2BProvider b2b)
                    result = EinvoiceProviderResult.Fail("b2b_register 僅支援 ecpay_b2b 平台");
                else
                    result = await b2b.RegisterCustomerAsync("22099131", "台積電股份有限公司", "diag@example.com", platform);
            }
            else
            {
                result = op switch
                {
                    "issue" => await provider.IssueAsync(dummy, platform),
                    "invalid" => await provider.InvalidAsync(dummy, asciiOnly ? "TestVoid" : "診斷測試作廢", platform),
                    "allowance" => await provider.AllowanceAsync(dummyAllowance, dummy, platform),
                    "allowance_invalid" => await provider.AllowanceInvalidAsync(dummyAllowance, asciiOnly ? "TestCancel" : "診斷測試作廢折讓", platform),
                    _ => EinvoiceProviderResult.Fail($"未知 op={op}")
                };
            }
            return Content(
                $"=== Diagnose [{code}] op={op} ===\n" +
                $"input.number={number ?? "(none)"} allowanceNumber={allowanceNumber ?? "(none)"}\n" +
                $"platform.IsSandbox: {platform.IsSandbox}\n" +
                $"\n--- Result ---\n" +
                $"Success: {result.Success}\n" +
                $"Code: {result.Code}\n" +
                $"Message: {result.Message}\n" +
                $"Number: {result.Number}\n" +
                $"Raw: {result.RawResponse}\n",
                "text/plain; charset=utf-8");
        }
        catch (Exception ex)
        {
            return Content(
                $"=== Diagnose [{code}] op={op} ===\n" +
                $"EXCEPTION: {ex.GetType().Name}\n{ex.Message}\n\n{ex.StackTrace}",
                "text/plain; charset=utf-8");
        }
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

    private static string GetGatewebSellerTaxId(EinvoicePlatform p)
    {
        // 優先使用 ExtraParams.sellerIdentifier；其次取 sellerDepartment 前綴
        if (!string.IsNullOrEmpty(p.ExtraParams))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(p.ExtraParams);
                if (doc.RootElement.TryGetProperty("sellerIdentifier", out var si) && si.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return si.GetString() ?? "23639781";
                }
                if (doc.RootElement.TryGetProperty("sellerDepartment", out var sd) && sd.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var s = sd.GetString() ?? "";
                    var idx = s.IndexOf('_');
                    return idx > 0 ? s[..idx] : s;
                }
            }
            catch { }
        }
        return "23639781"; // Gateweb 文件範例
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPlatform(
        int id, string? merchantId, string? apiKey, string? apiSecret, bool isSandbox,
        string? sellerIdentifier = null, string? sellerName = null, string? sellerDepartment = null)
    {
        var existing = await _context.EinvoicePlatforms.FindAsync(id);
        if (existing == null) return NotFound();

        existing.MerchantId = merchantId;
        existing.ApiKey = apiKey;
        existing.ApiSecret = apiSecret;
        existing.IsSandbox = isSandbox;
        existing.UpdatedAt = DateTime.UtcNow;

        // Gateweb 賣方資訊：合併進 ExtraParams JSON（保留其他現存 key）
        if (existing.Code == "gateweb")
        {
            var extra = new Dictionary<string, string?>();
            if (!string.IsNullOrWhiteSpace(existing.ExtraParams))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(existing.ExtraParams);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                        if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                            extra[prop.Name] = prop.Value.GetString();
                }
                catch { }
            }
            extra["sellerIdentifier"] = string.IsNullOrWhiteSpace(sellerIdentifier) ? null : sellerIdentifier.Trim();
            extra["sellerName"] = string.IsNullOrWhiteSpace(sellerName) ? null : sellerName.Trim();
            extra["sellerDepartment"] = string.IsNullOrWhiteSpace(sellerDepartment) ? null : sellerDepartment.Trim();
            // 移除空值，避免 JSON 留 null
            var compact = extra.Where(kv => !string.IsNullOrEmpty(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value!);
            existing.ExtraParams = compact.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(compact)
                : null;
        }

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

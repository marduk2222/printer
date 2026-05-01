using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;
using printer.Models;
using printer.Services;

namespace printer.Controllers;

public class InvoiceController : Controller
{
    private readonly PrinterDbContext _context;
    private readonly IBillingService _billingService;

    public InvoiceController(PrinterDbContext context, IBillingService billingService)
    {
        _context = context;
        _billingService = billingService;
    }

    public async Task<IActionResult> Index(int? partnerId, string? status, int invoiceDays = 7)
    {
        invoiceDays = Math.Max(1, Math.Min(invoiceDays, 365));

        var invoices = await _billingService.GetInvoicesAsync(partnerId, status);

        // 查詢每張帳單是否已開立發票
        var invoiceIds = invoices.Select(i => i.Id).ToList();
        var einvoiceMap = await _context.Einvoices
            .Where(e => e.BillingInvoiceId != null && invoiceIds.Contains(e.BillingInvoiceId.Value))
            .GroupBy(e => e.BillingInvoiceId!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.OrderByDescending(e => e.CreatedAt).First());
        ViewBag.EinvoiceMap = einvoiceMap;

        // 發票模組是否啟用
        var einvoiceModule = await _context.SystemModules.FirstOrDefaultAsync(m => m.Code == "einvoice");
        ViewBag.EinvoiceEnabled = einvoiceModule?.IsEnabled == true;

        // 帳單風格模組是否啟用
        var billingStyleModule = await _context.SystemModules.FirstOrDefaultAsync(m => m.Code == "billingstyle");
        ViewBag.BillingStyleEnabled = billingStyleModule?.IsEnabled == true;

        ViewBag.Partners = new SelectList(
            await _context.Partners.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name", partnerId);
        ViewBag.PartnerId = partnerId;
        ViewBag.Status = status;

        // 未付帳單
        var unpaidInvoices = await _context.Invoices
            .Include(i => i.Partner)
            .Where(i => i.Status == "draft" || i.Status == "confirmed")
            .OrderBy(i => i.CreatedAt)
            .Take(30)
            .ToListAsync();
        ViewBag.UnpaidInvoices = unpaidInvoices;

        // 近期帳單
        var invoiceCutoff = DateTime.UtcNow.AddDays(-invoiceDays);
        var recentInvoices = await _context.Invoices
            .Include(i => i.Partner)
            .Where(i => i.CreatedAt >= invoiceCutoff)
            .OrderByDescending(i => i.CreatedAt)
            .Take(30)
            .ToListAsync();
        ViewBag.RecentInvoices = recentInvoices;
        ViewBag.InvoiceDays = invoiceDays;

        return View(invoices);
    }

    public async Task<IActionResult> Details(int id)
    {
        var invoice = await _billingService.GetInvoiceAsync(id);
        if (invoice == null) return NotFound();

        // 發票模組是否啟用
        var einvoiceModule = await _context.SystemModules
            .FirstOrDefaultAsync(m => m.Code == "einvoice");
        ViewBag.EinvoiceEnabled = einvoiceModule?.IsEnabled == true;

        // 帳單風格模組是否啟用
        var billingStyleModule = await _context.SystemModules
            .FirstOrDefaultAsync(m => m.Code == "billingstyle");
        ViewBag.BillingStyleEnabled = billingStyleModule?.IsEnabled == true;

        // 啟用的發票平台
        var activePlatform = await _context.EinvoicePlatforms
            .FirstOrDefaultAsync(p => p.IsActive);
        ViewBag.ActivePlatform = activePlatform;

        // 此帳單已開立的發票
        var einvoices = await _context.Einvoices
            .Where(e => e.BillingInvoiceId == id)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
        ViewBag.Einvoices = einvoices;

        return View(invoice);
    }

    public async Task<IActionResult> Print(int id, string? style = null)
    {
        var invoice = await _billingService.GetInvoiceAsync(id);
        if (invoice == null) return NotFound();

        var settings = await _context.InvoicePrintSettings.FirstOrDefaultAsync()
                       ?? new printer.Data.Entities.InvoicePrintSettings();

        // 若有傳入 style 參數則覆蓋全域設定
        var effectiveStyle = !string.IsNullOrEmpty(style) ? style : settings.TemplateCode;

        var vm = new InvoicePrintViewModel { Invoice = invoice, Settings = settings };

        var viewName = effectiveStyle switch
        {
            "modern"          => "PrintModern",
            "traditional"     => "PrintTraditional",
            "elegant"         => "PrintElegant",
            "corporate"       => "PrintCorporate",
            "minimal"         => "PrintMinimal",
            "warm"            => "PrintWarm",
            "tech"            => "PrintTech",
            "nature"          => "PrintNature",
            "gradient"        => "PrintGradient",
            "compact"         => "PrintCompact",
            "colorful"        => "PrintColorful",
            "retro"           => "PrintRetro",
            "ocean"           => "PrintOcean",
            "sunset"          => "PrintSunset",
            // 帳單風格模組 - 新增 15 種風格
            "scandinavian"    => "PrintScandinavian",
            "japanese"        => "PrintJapanese",
            "mediterranean"   => "PrintMediterranean",
            "cyberpunk"       => "PrintCyberpunk",
            "marble"          => "PrintMarble",
            "arctic"          => "PrintArctic",
            "rose"            => "PrintRose",
            "slate"           => "PrintSlate",
            "teal"            => "PrintTeal",
            "crimson"         => "PrintCrimson",
            "silver"          => "PrintSilver",
            "lavender"        => "PrintLavender",
            "midnight"        => "PrintMidnight",
            "emerald"         => "PrintEmerald",
            "amber"           => "PrintAmber",
            // 版面格式
            "bigheader"       => "PrintBigHeader",
            "rightbar"        => "PrintRightBar",
            "split"           => "PrintSplit",
            "landscape"       => "PrintLandscape",
            "twostub"         => "PrintTwoStub",
            "card"            => "PrintCard",
            "report"          => "PrintReport",
            "tabular"         => "PrintTabular",
            "dark"            => "PrintDark",
            "stacked"         => "PrintStacked",
            "formal"          => "PrintFormal",
            "lines"           => "PrintLines",
            "mono"            => "PrintMono",
            "frame"           => "PrintFrame",
            "gridtop"         => "PrintGridTop",
            "serif"           => "PrintSerif",
            "dense"           => "PrintDense",
            "accent"          => "PrintAccent",
            "swiss"           => "PrintSwiss",
            "stamp"           => "PrintStamp",
            "invoicefirst"    => "PrintInvoiceFirst",
            "hero"            => "PrintHero",
            "centered"        => "PrintCentered",
            _                 => "PrintClassic"
        };

        return View(viewName, vm);
    }

    [HttpPost]
    public async Task<IActionResult> Confirm(int id)
    {
        if (await _billingService.ConfirmInvoiceAsync(id))
            TempData["Success"] = "帳單已確認";
        else
            TempData["Error"] = "無法確認帳單";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> MarkPaid(int id)
    {
        if (await _billingService.MarkAsPaidAsync(id))
            TempData["Success"] = "帳單已標記為已付款";
        else
            TempData["Error"] = "無法標記帳單";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        if (await _billingService.CancelInvoiceAsync(id))
            TempData["Success"] = "帳單已取消";
        else
            TempData["Error"] = "無法取消帳單";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, DateOnly periodStart, DateOnly periodEnd, string? note)
    {
        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null) return NotFound();
        if (invoice.Status != "draft")
        {
            TempData["Error"] = "只能編輯草稿狀態的帳單";
            return RedirectToAction(nameof(Details), new { id });
        }
        var hasIssuedEinvoice = await _context.Einvoices
            .AnyAsync(e => e.BillingInvoiceId == id && e.Status == "issued");
        if (hasIssuedEinvoice)
        {
            TempData["Error"] = "已開立發票的帳單無法修改";
            return RedirectToAction(nameof(Details), new { id });
        }

        invoice.PeriodStart = periodStart;
        invoice.PeriodEnd = periodEnd;
        invoice.BillingPeriod = $"{periodStart:yyyyMMdd}-{periodEnd:yyyyMMdd}";
        invoice.Note = note;
        invoice.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        TempData["Success"] = "帳單已更新";
        return RedirectToAction(nameof(Details), new { id });
    }

    #region 手動建立帳單

    public async Task<IActionResult> Create()
    {
        ViewBag.Partners = new SelectList(
            await _context.Partners.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name");

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int partnerId,
        DateOnly periodStart,
        DateOnly periodEnd,
        string? note,
        List<ManualInvoiceItemInput> items)
    {
        try
        {
            if (!items.Any(i => i.Subtotal > 0))
            {
                TempData["Error"] = "請至少新增一筆明細";
                return RedirectToAction(nameof(Create));
            }

            // 產生帳單編號
            var period = $"{periodStart:yyyyMMdd}-{periodEnd:yyyyMMdd}";
            var count = await _context.Invoices.CountAsync(i =>
                i.InvoiceNumber.StartsWith($"INV-{periodStart:yyyyMM}"));
            var invoiceNumber = $"INV-{periodStart:yyyyMM}-{(count + 1):D3}";

            var invoice = new Invoice
            {
                InvoiceNumber = invoiceNumber,
                PartnerId = partnerId,
                BillingPeriod = period,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                Status = "draft",
                Note = note,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            foreach (var item in items.Where(i => i.Subtotal > 0))
            {
                invoice.Items.Add(new InvoiceItem
                {
                    PrinterId = item.PrinterId,
                    BillingType = Data.Enums.BillingType.Monthly,
                    Description = item.Description ?? "",
                    BlackPages = item.BlackPages,
                    ColorPages = item.ColorPages,
                    LargePages = item.LargePages,
                    MonthlyFee = item.MonthlyFee,
                    PageFee = item.PageFee,
                    Subtotal = item.Subtotal
                });
            }

            invoice.TotalAmount = invoice.Items.Sum(i => i.Subtotal);
            invoice.TaxAmount = 0;
            invoice.GrandTotal = invoice.TotalAmount;

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"帳單 {invoice.InvoiceNumber} 已建立";
            return RedirectToAction(nameof(Details), new { id = invoice.Id });
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Create));
        }
    }

    #endregion
}

public class ManualInvoiceItemInput
{
    public int PrinterId { get; set; }
    public string? Description { get; set; }
    public int BlackPages { get; set; }
    public int ColorPages { get; set; }
    public int LargePages { get; set; }
    public decimal MonthlyFee { get; set; }
    public decimal PageFee { get; set; }
    public decimal Subtotal { get; set; }
}


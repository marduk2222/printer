using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;
using printer.Data.Enums;
using printer.Models;
using printer.Services;

namespace printer.Controllers;

[Authorize(Roles = "admin,supervisor")]
public class InvoicePrintSettingsController : Controller
{
    private readonly PrinterDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IModuleService _moduleService;

    public InvoicePrintSettingsController(PrinterDbContext context, IWebHostEnvironment env, IModuleService moduleService)
    {
        _context = context;
        _env = env;
        _moduleService = moduleService;
    }

    public async Task<IActionResult> Index()
    {
        var settings = await _context.InvoicePrintSettings.FirstOrDefaultAsync()
                       ?? new InvoicePrintSettings { TemplateCode = "classic" };
        ViewBag.BillingstyleEnabled = await _moduleService.IsModuleEnabledAsync("billingstyle");
        return View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(InvoicePrintSettings settings, IFormFile? logoFile)
    {
        try
        {
            // 處理 Logo 圖片上傳
            if (logoFile != null && logoFile.Length > 0)
            {
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg" };
                var ext = Path.GetExtension(logoFile.FileName).ToLowerInvariant();
                if (!allowed.Contains(ext))
                {
                    TempData["Error"] = "僅支援 JPG、PNG、GIF、WebP、SVG 格式";
                    return RedirectToAction(nameof(Index));
                }

                var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "logos");
                Directory.CreateDirectory(uploadDir);

                // 刪除舊 Logo（若為本站上傳的）
                var existing0 = await _context.InvoicePrintSettings.FirstOrDefaultAsync();
                if (existing0?.CompanyLogoUrl?.StartsWith("/uploads/logos/") == true)
                {
                    var oldPath = Path.Combine(_env.WebRootPath, existing0.CompanyLogoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                var fileName = $"logo_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
                var filePath = Path.Combine(uploadDir, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await logoFile.CopyToAsync(stream);

                settings.CompanyLogoUrl = $"/uploads/logos/{fileName}";
            }

            var existing = await _context.InvoicePrintSettings.FirstOrDefaultAsync();
            if (existing == null)
            {
                _context.InvoicePrintSettings.Add(settings);
            }
            else
            {
                existing.TemplateCode = settings.TemplateCode;
                existing.CompanyName = settings.CompanyName;
                existing.CompanySlogan = settings.CompanySlogan;
                existing.CompanyAddress = settings.CompanyAddress;
                existing.CompanyPhone = settings.CompanyPhone;
                existing.CompanyEmail = settings.CompanyEmail;
                existing.CompanyWebsite = settings.CompanyWebsite;
                // 只有上傳新圖才更新 URL，否則保留原值
                if (logoFile != null && logoFile.Length > 0)
                    existing.CompanyLogoUrl = settings.CompanyLogoUrl;
                existing.BankName = settings.BankName;
                existing.BankBranch = settings.BankBranch;
                existing.BankAccount = settings.BankAccount;
                existing.PaymentTerms = settings.PaymentTerms;
                existing.FooterNote = settings.FooterNote;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "帳單列印設定已儲存";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "儲存失敗：" + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Preview(string templateCode)
    {
        var settings = await _context.InvoicePrintSettings.FirstOrDefaultAsync()
                       ?? new InvoicePrintSettings { TemplateCode = "classic" };

        // 產生示範帳單（假資料）
        var demoInvoice = new Invoice
        {
            Id = 0,
            InvoiceNumber = "INV-DEMO-001",
            BillingPeriod = "20260301-20260331",
            PeriodStart = new DateOnly(2026, 3, 1),
            PeriodEnd = new DateOnly(2026, 3, 31),
            Status = "confirmed",
            ConfirmedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            TotalAmount = 12500,
            TaxAmount = 625,
            GrandTotal = 13125,
            Partner = new Partner
            {
                Name = "範例企業股份有限公司",
                ContactName = "王小明",
                Phone = "02-2345-6789",
                Email = "contact@example.com",
                Address = "台北市信義區信義路五段 7 號"
            },
            Items = new List<InvoiceItem>
            {
                new() { Description = "A3 多功能事務機 (SN: MFP-001)", BillingType = BillingType.PerPage,
                        BlackPages = 3200, ColorPages = 850, MonthlyFee = 2000, PageFee = 4375, Subtotal = 6375 },
                new() { Description = "雷射印表機 (SN: LZR-042)", BillingType = BillingType.PerPage,
                        BlackPages = 1800, ColorPages = 200, MonthlyFee = 1500, PageFee = 2600, Subtotal = 4100 },
                new() { Description = "彩色複合機 (SN: CLR-007)", BillingType = BillingType.Monthly,
                        MonthlyFee = 2025, PageFee = 0, Subtotal = 2025 },
            }
        };

        var vm = new InvoicePrintViewModel { Invoice = demoInvoice, Settings = settings };

        var viewName = templateCode switch
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
            // 簽名版 10 款
            "sign-formal"     => "PrintSignFormal",
            "sign-contract"   => "PrintSignContract",
            "sign-delivery"   => "PrintSignDelivery",
            "sign-receipt"    => "PrintSignReceipt",
            "sign-witness"    => "PrintSignWitness",
            "sign-seal"       => "PrintSignSeal",
            "sign-compact"    => "PrintSignCompact",
            "sign-modern"     => "PrintSignModern",
            "sign-bordered"   => "PrintSignBordered",
            "sign-report"     => "PrintSignReport",
            "sign-dual"       => "PrintSignDual",
            "sign-acknowledge" => "PrintSignAcknowledge",
            _                 => "PrintClassic"
        };

        // 使用 Invoice 的 View
        return View($"~/Views/Invoice/{viewName}.cshtml", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActivateTemplate(string templateCode)
    {
        var existing = await _context.InvoicePrintSettings.FirstOrDefaultAsync();
        if (existing == null)
        {
            _context.InvoicePrintSettings.Add(new InvoicePrintSettings { TemplateCode = templateCode });
        }
        else
        {
            existing.TemplateCode = templateCode;
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "已切換列印樣式";
        return RedirectToAction(nameof(Index));
    }
}

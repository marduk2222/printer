using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Models;
using printer.Services;
using System.Diagnostics;

namespace printer.Controllers;

public class HomeController : Controller
{
    private readonly PrinterDbContext _context;
    private readonly ILogger<HomeController> _logger;
    private readonly IModuleService _moduleService;

    public HomeController(PrinterDbContext context, ILogger<HomeController> logger, IModuleService moduleService)
    {
        _context = context;
        _logger = logger;
        _moduleService = moduleService;
    }

    public async Task<IActionResult> Index(int invoiceDays = 7)
    {
        invoiceDays = Math.Max(1, Math.Min(invoiceDays, 365));
        var today = DateOnly.FromDateTime(DateTime.Today);

        var billingEnabled = await _moduleService.IsModuleEnabledAsync("billing");
        ViewBag.BillingEnabled = billingEnabled;

        // 最近 7 天有回報的設備 ID
        var sevenDaysAgo = today.AddDays(-7);
        var recentPrinterIds = await _context.PrintRecords
            .Where(r => r.Date >= sevenDaysAgo)
            .Select(r => r.PrinterId)
            .Distinct()
            .ToListAsync();

        // 統計資料
        var stats = new DashboardStats
        {
            TotalPrinters = await _context.Printers.CountAsync(p => p.IsActive),
            TotalPrintersInUse = await _context.Printers.CountAsync(p => p.IsActive),
            ActiveAlerts = await _context.AlertRecords.CountAsync(a => a.State != "resolved"),
            ReportedPrinters = recentPrinterIds.Count,
        };

        // 有異常的設備 (有未解決告警的設備)
        var alertPrinters = await _context.Printers
            .Include(p => p.Partner)
            .Include(p => p.AlertRecords.Where(a => a.State != "resolved"))
            .Where(p => p.AlertRecords.Any(a => a.State != "resolved"))
            .OrderByDescending(p => p.AlertRecords.Count(a => a.State != "resolved"))
            .Take(20)
            .ToListAsync();

        // 耗材不足的設備 (根據每台設備的自訂閾值)
        var lowSupplyPrinters = await _context.Printers
            .Include(p => p.Partner)
            .Include(p => p.Model)
            .Where(p => p.IsActive)
            .ToListAsync();

        // 在記憶體中過濾 (使用每台設備的自訂閾值)
        lowSupplyPrinters = lowSupplyPrinters
            .Where(p => p.HasLowSupply)
            .OrderBy(p => p.TonerBlack ?? 100)
            .Take(20)
            .ToList();

        // 最近未回報的設備 (超過 7 天沒有抄表記錄)
        var offlinePrinters = await _context.Printers
            .Include(p => p.Partner)
            .Where(p => p.IsActive && !recentPrinterIds.Contains(p.Id))
            .Take(20)
            .ToListAsync();

        // 合約即將到期或已到期的設備
        var allContractPrinters = await _context.Printers
            .Include(p => p.Partner)
            .Where(p => p.IsActive && p.ContractEndDate.HasValue)
            .ToListAsync();

        var contractAlertPrinters = allContractPrinters
            .Where(p => p.IsContractExpiringSoon || p.IsContractExpired)
            .OrderBy(p => p.ContractEndDate)
            .Take(20)
            .ToList();

        stats.ContractAlertCount = contractAlertPrinters.Count;
        stats.LowSupplyCount = lowSupplyPrinters.Count;

        // 帳單相關資料（僅在 billing 模組啟用時查詢）
        var unpaidInvoices = new List<printer.Data.Entities.Invoice>();
        var recentInvoices = new List<printer.Data.Entities.Invoice>();
        if (billingEnabled)
        {
            unpaidInvoices = await _context.Invoices
                .Include(i => i.Partner)
                .Where(i => i.Status == "draft" || i.Status == "confirmed")
                .OrderBy(i => i.CreatedAt)
                .Take(30)
                .ToListAsync();

            var invoiceCutoff = DateTime.UtcNow.AddDays(-invoiceDays);
            recentInvoices = await _context.Invoices
                .Include(i => i.Partner)
                .Where(i => i.CreatedAt >= invoiceCutoff)
                .OrderByDescending(i => i.CreatedAt)
                .Take(30)
                .ToListAsync();
        }
        stats.UnpaidInvoiceCount = unpaidInvoices.Count;
        stats.RecentInvoiceCount = recentInvoices.Count;

        ViewBag.Stats = stats;
        ViewBag.AlertPrinters = alertPrinters;
        ViewBag.LowSupplyPrinters = lowSupplyPrinters;
        ViewBag.OfflinePrinters = offlinePrinters;
        ViewBag.ContractAlertPrinters = contractAlertPrinters;
        ViewBag.UnpaidInvoices = unpaidInvoices;
        ViewBag.RecentInvoices = recentInvoices;
        ViewBag.InvoiceDays = invoiceDays;

        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

public class DashboardStats
{
    public int TotalPrinters { get; set; }
    public int TotalPrintersInUse { get; set; }
    public int ActiveAlerts { get; set; }
    public int ReportedPrinters { get; set; }
    public int ContractAlertCount { get; set; }
    public int LowSupplyCount { get; set; }
    public int UnpaidInvoiceCount { get; set; }
    public int RecentInvoiceCount { get; set; }
}

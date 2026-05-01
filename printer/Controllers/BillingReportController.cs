using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Services;

namespace printer.Controllers;

public class BillingReportController : Controller
{
    private readonly PrinterDbContext _context;
    private readonly IBillingService _billingService;

    public BillingReportController(PrinterDbContext context, IBillingService billingService)
    {
        _context = context;
        _billingService = billingService;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.Partners = new SelectList(
            await _context.Partners.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name");

        var today = DateTime.Today;
        ViewBag.DefaultStart = new DateOnly(today.Year, today.Month, 1).ToString("yyyy-MM-dd");
        ViewBag.DefaultEnd = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd");

        return View();
    }

    /// <summary>
    /// 計費預覽：即時計算（不依賴已生成帳單）
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Preview(int? partnerId, DateOnly startDate, DateOnly endDate)
    {
        List<int> printerIds;
        if (partnerId.HasValue)
        {
            printerIds = await _context.Printers
                .Where(p => p.PartnerId == partnerId.Value && p.IsActive)
                .Select(p => p.Id)
                .ToListAsync();
        }
        else
        {
            printerIds = await _context.PrinterBillingConfigs
                .Include(c => c.Printer)
                .Where(c => c.IsEnabled && c.Printer != null && c.Printer.IsActive)
                .Select(c => c.PrinterId)
                .ToListAsync();
        }

        var calculations = new List<BillingCalculation>();
        foreach (var pid in printerIds)
        {
            var calc = await _billingService.CalculateAsync(pid, startDate, endDate);
            calculations.Add(calc);
        }

        string label;
        if (partnerId.HasValue)
        {
            var partner = await _context.Partners.FindAsync(partnerId.Value);
            label = partner?.Name ?? "";
        }
        else
        {
            label = "全部客戶";
        }

        ViewBag.ReportTitle = $"{label} - {startDate:yyyy/MM/dd} ~ {endDate:yyyy/MM/dd} 計費預覽";
        ViewBag.StartDate = startDate.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate.ToString("yyyy-MM-dd");
        ViewBag.PartnerId = partnerId;
        ViewBag.Partners = new SelectList(
            await _context.Partners.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name", partnerId);

        return View("Preview", calculations);
    }

    [HttpPost]
    public async Task<IActionResult> Generate(int? partnerId, DateOnly startDate, DateOnly endDate)
    {
        var periodLabel = $"{startDate:yyyy/MM/dd} ~ {endDate:yyyy/MM/dd}";

        // 從帳單查詢
        var invoiceQuery = _context.Invoices
            .Include(i => i.Partner)
            .Include(i => i.Items)
                .ThenInclude(item => item.Printer)
            .Where(i => i.Status != "cancelled")
            .Where(i => i.PeriodStart <= endDate && i.PeriodEnd >= startDate);

        if (partnerId.HasValue)
        {
            invoiceQuery = invoiceQuery.Where(i => i.PartnerId == partnerId.Value);
        }

        var invoices = await invoiceQuery
            .OrderByDescending(i => i.PeriodStart)
            .ToListAsync();

        string reportTitle;
        if (partnerId.HasValue)
        {
            var partner = await _context.Partners.FindAsync(partnerId.Value);
            reportTitle = $"{partner?.Name} - {periodLabel} 計費報表";
        }
        else
        {
            reportTitle = $"全部客戶 - {periodLabel} 計費報表";
        }

        ViewBag.ReportTitle = reportTitle;
        ViewBag.StartDate = startDate;
        ViewBag.EndDate = endDate;
        ViewBag.PartnerId = partnerId;

        ViewBag.Partners = new SelectList(
            await _context.Partners.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name", partnerId);
        ViewBag.DefaultStart = startDate.ToString("yyyy-MM-dd");
        ViewBag.DefaultEnd = endDate.ToString("yyyy-MM-dd");

        return View("Report", invoices);
    }

    [HttpPost]
    public async Task<IActionResult> GenerateInvoice(int? partnerId, DateOnly startDate, DateOnly endDate)
    {
        try
        {
            if (partnerId.HasValue)
            {
                var invoice = await _billingService.GenerateInvoiceAsync(partnerId.Value, startDate, endDate);
                TempData["Success"] = $"帳單 {invoice.InvoiceNumber} 已生成（草稿）";
            }
            else
            {
                var invoices = await _billingService.BatchGenerateInvoicesAsync(startDate, endDate);
                TempData["Success"] = $"已生成 {invoices.Count} 張帳單（草稿）";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Index", "Invoice");
    }

    public async Task<IActionResult> Summary(int? partnerId)
    {
        var endDate = DateTime.Today;
        var startDate = endDate.AddMonths(-11);
        var startDateOnly = DateOnly.FromDateTime(startDate.AddDays(1 - startDate.Day));
        var endDateOnly = DateOnly.FromDateTime(endDate);

        // 每月列印張數統計
        var printQuery = _context.PrintRecords
            .Where(r => r.Date >= startDateOnly && r.Date <= endDateOnly);

        if (partnerId.HasValue)
        {
            printQuery = printQuery.Where(r => r.PartnerId == partnerId.Value);
        }

        var monthlyPrintRecords = await printQuery
            .ToListAsync();

        var monthlyPrintSummary = monthlyPrintRecords
            .GroupBy(r => r.Date.ToString("yyyy/MM"))
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Period = g.Key,
                BlackPages = g.Sum(r => r.BlackSheets),
                ColorPages = g.Sum(r => r.ColorSheets),
                LargePages = g.Sum(r => r.LargeSheets),
                TotalPages = g.Sum(r => r.BlackSheets + r.ColorSheets + r.LargeSheets)
            })
            .ToList();

        // 帳單統計
        var invoiceQuery = _context.Invoices
            .Where(i => i.Status != "cancelled")
            .Where(i => i.CreatedAt >= startDate)
            .Include(i => i.Partner)
            .AsQueryable();

        if (partnerId.HasValue)
        {
            invoiceQuery = invoiceQuery.Where(i => i.PartnerId == partnerId.Value);
        }

        var invoices = await invoiceQuery.ToListAsync();

        var monthlySummary = invoices
            .GroupBy(i => i.PeriodStart.ToString("yyyy/MM"))
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Period = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(i => i.GrandTotal),
                PaidAmount = g.Where(i => i.Status == "paid").Sum(i => i.GrandTotal),
                UnpaidAmount = g.Where(i => i.Status != "paid").Sum(i => i.GrandTotal)
            })
            .ToList();

        var partnerSummary = invoices
            .GroupBy(i => i.Partner?.Name ?? "未知")
            .OrderByDescending(g => g.Sum(i => i.GrandTotal))
            .Select(g => new
            {
                Partner = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(i => i.GrandTotal),
                PaidAmount = g.Where(i => i.Status == "paid").Sum(i => i.GrandTotal)
            })
            .ToList();

        ViewBag.MonthlyPrintSummary = monthlyPrintSummary;
        ViewBag.TotalBlackPages = monthlyPrintRecords.Sum(r => r.BlackSheets);
        ViewBag.TotalColorPages = monthlyPrintRecords.Sum(r => r.ColorSheets);
        ViewBag.TotalLargePages = monthlyPrintRecords.Sum(r => r.LargeSheets);
        ViewBag.MonthlySummary = monthlySummary;
        ViewBag.PartnerSummary = partnerSummary;
        ViewBag.TotalInvoices = invoices.Count;
        ViewBag.TotalAmount = invoices.Sum(i => i.GrandTotal);
        ViewBag.PaidAmount = invoices.Where(i => i.Status == "paid").Sum(i => i.GrandTotal);
        ViewBag.PartnerId = partnerId;
        ViewBag.Partners = new SelectList(
            await _context.Partners.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name", partnerId);

        return View();
    }
}

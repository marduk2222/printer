using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;
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

    [HttpPost]
    public async Task<IActionResult> Generate(int? partnerId, DateOnly startDate, DateOnly endDate)
    {
        var periodLabel = $"{startDate:yyyy/MM/dd} ~ {endDate:yyyy/MM/dd}";

        // 從帳單查詢
        var invoiceQuery = _context.Invoices
            .Include(i => i.Partner)
            .Include(i => i.Items)
                .ThenInclude(item => item.Printer)
            .Include(i => i.Items)
                .ThenInclude(item => item.BillingGroup)
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
            reportTitle = $"{partner?.Name} - {periodLabel} 帳單報表";
        }
        else
        {
            reportTitle = $"全部客戶 - {periodLabel} 帳單報表";
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
            .Include(r => r.Values).ThenInclude(v => v.SheetType)
            .ToListAsync();

        // 用 SheetType 名稱對應「黑白/彩色/大張」分類；其餘 SheetType 計入 OtherPages
        var blackNames = new HashSet<string> { "黑白" };
        var colorNames = new HashSet<string> { "彩色" };
        var largeNames = new HashSet<string> { "大張", "彩色大張" };
        int SumByNames(PrintRecord r, HashSet<string> names) =>
            r.Values.Where(v => v.SheetType != null && names.Contains(v.SheetType!.Name)).Sum(v => v.Value);

        var monthlyPrintSummary = monthlyPrintRecords
            .GroupBy(r => r.Date.ToString("yyyy/MM"))
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Period = g.Key,
                BlackPages = g.Sum(r => SumByNames(r, blackNames)),
                ColorPages = g.Sum(r => SumByNames(r, colorNames)),
                LargePages = g.Sum(r => SumByNames(r, largeNames)),
                TotalPages = g.Sum(r => r.Values.Sum(v => v.Value))
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
        ViewBag.TotalBlackPages = monthlyPrintRecords.Sum(r => SumByNames(r, blackNames));
        ViewBag.TotalColorPages = monthlyPrintRecords.Sum(r => SumByNames(r, colorNames));
        ViewBag.TotalLargePages = monthlyPrintRecords.Sum(r => SumByNames(r, largeNames));
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

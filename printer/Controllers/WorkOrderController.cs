using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;
using printer.Services;

namespace printer.Controllers;

[Authorize]
public class WorkOrderController : Controller
{
    private readonly PrinterDbContext _context;
    private readonly IWorkOrderService _workOrderService;

    public WorkOrderController(PrinterDbContext context, IWorkOrderService workOrderService)
    {
        _context = context;
        _workOrderService = workOrderService;
    }

    public async Task<IActionResult> Index(
        int? partnerId, int? printerId, string? status,
        DateTime? dateFrom, DateTime? dateTo, bool? clearDates)
    {
        // 預設日期範圍：近一個月（使用者未設定且未主動清除時套用）
        if (clearDates != true && !dateFrom.HasValue && !dateTo.HasValue)
        {
            dateTo = DateTime.Now.Date;
            dateFrom = dateTo.Value.AddMonths(-1);
        }

        var workOrders = await _workOrderService.GetListAsync(partnerId, printerId, status, dateFrom, dateTo);

        ViewBag.Partners = new SelectList(
            await _context.Partners.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name", partnerId);
        ViewBag.PartnerId = partnerId;
        ViewBag.PrinterId = printerId;
        ViewBag.Status = status;
        ViewBag.DateFrom = dateFrom;
        ViewBag.DateTo = dateTo;

        // 卡片統計與下方資料連動：套用除 status 以外的全部篩選
        var statsScope = await _workOrderService.GetListAsync(partnerId, printerId, null, dateFrom, dateTo);
        ViewBag.CountOpen = statsScope.Count(w => w.Status == "open");
        ViewBag.CountInProgress = statsScope.Count(w => w.Status == "in_progress");
        ViewBag.CountCompleted = statsScope.Count(w => w.Status == "completed");
        ViewBag.CountAll = statsScope.Count;

        return View(workOrders);
    }

    public async Task<IActionResult> Details(int id)
    {
        var workOrder = await _workOrderService.GetAsync(id);
        if (workOrder == null) return NotFound();
        return View(workOrder);
    }

    public async Task<IActionResult> Create(int? partnerId)
    {
        await PopulateSelectLists();
        var workOrder = new WorkOrder { PartnerId = partnerId };
        return View(workOrder);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        WorkOrder workOrder,
        List<int>? printerIds,
        List<int>? userIds,
        List<IFormFile>? images,
        string? imageCaption)
    {
        if (string.IsNullOrWhiteSpace(workOrder.Title))
            ModelState.AddModelError("Title", "請填寫標題");

        if (!ModelState.IsValid)
        {
            await PopulateSelectLists();
            return View(workOrder);
        }

        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var creator = await _context.AppUsers.FirstOrDefaultAsync(u => u.Username == username);
        workOrder.CreatedById = creator?.Id;

        var created = await _workOrderService.CreateAsync(workOrder, printerIds, userIds);

        if (images != null)
        {
            foreach (var file in images.Where(f => f.Length > 0))
            {
                try { await _workOrderService.AddImageAsync(created.Id, file, imageCaption); }
                catch (ArgumentException ex) { TempData["Error"] = ex.Message; }
            }
        }

        TempData["Success"] = $"派工單 {created.OrderNumber} 已建立";
        return RedirectToAction(nameof(Details), new { id = created.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var workOrder = await _workOrderService.GetAsync(id);
        if (workOrder == null) return NotFound();

        if (workOrder.Status == "completed" || workOrder.Status == "cancelled")
        {
            TempData["Error"] = "已完成或已取消的派工單無法編輯";
            return RedirectToAction(nameof(Details), new { id });
        }

        await PopulateSelectLists(
            workOrder.WorkOrderPrinters.Select(p => p.PrinterId).ToList(),
            workOrder.WorkOrderAssignees.Select(a => a.UserId).ToList());
        return View(workOrder);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        WorkOrder workOrder,
        List<int>? printerIds,
        List<int>? userIds)
    {
        if (id != workOrder.Id) return BadRequest();
        if (string.IsNullOrWhiteSpace(workOrder.Title))
            ModelState.AddModelError("Title", "請填寫標題");

        if (!ModelState.IsValid)
        {
            await PopulateSelectLists(printerIds, userIds);
            return View(workOrder);
        }

        try
        {
            await _workOrderService.UpdateAsync(workOrder, printerIds, userIds);
            TempData["Success"] = "派工單已更新";
        }
        catch (ArgumentException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImage(int id, List<IFormFile> images, string? caption)
    {
        if (images == null || !images.Any(f => f.Length > 0))
        {
            TempData["Error"] = "請選擇圖片";
            return RedirectToAction(nameof(Details), new { id });
        }

        foreach (var file in images.Where(f => f.Length > 0))
        {
            try { await _workOrderService.AddImageAsync(id, file, caption); }
            catch (ArgumentException ex) { TempData["Error"] = ex.Message; }
        }

        TempData["Success"] = "圖片已上傳";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status, string? note)
    {
        var validStatuses = new[] { "open", "in_progress", "completed", "cancelled" };
        if (!validStatuses.Contains(status))
        {
            TempData["Error"] = "無效的狀態";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (await _workOrderService.UpdateStatusAsync(id, status, note))
        {
            var labels = new Dictionary<string, string>
            {
                ["open"] = "待處理", ["in_progress"] = "處理中",
                ["completed"] = "已完成", ["cancelled"] = "已取消"
            };
            TempData["Success"] = $"狀態已更新為「{labels.GetValueOrDefault(status, status)}」";
        }
        else TempData["Error"] = "無法更新狀態";

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int imageId, int workOrderId)
    {
        await _workOrderService.DeleteImageAsync(imageId);
        TempData["Success"] = "圖片已刪除";
        return RedirectToAction(nameof(Details), new { id = workOrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Delete(int id)
    {
        if (await _workOrderService.DeleteAsync(id))
            TempData["Success"] = "派工單已刪除";
        else
            TempData["Error"] = "找不到派工單";
        return RedirectToAction(nameof(Index));
    }

    #region Excel 匯出 / 下載範本（不支援匯入）

    public async Task<IActionResult> Export(
        [FromQuery] int[]? ids,
        int? partnerId, int? printerId, string? status,
        DateTime? dateFrom, DateTime? dateTo)
    {
        var query = _context.WorkOrders
            .Include(w => w.Partner)
            .AsQueryable();

        if (ids != null && ids.Length > 0)
        {
            query = query.Where(w => ids.Contains(w.Id));
        }
        else
        {
            if (partnerId.HasValue)
                query = query.Where(w => w.PartnerId == partnerId.Value);
            if (printerId.HasValue)
                query = query.Where(w => w.WorkOrderPrinters.Any(p => p.PrinterId == printerId.Value));
            if (!string.IsNullOrEmpty(status))
                query = query.Where(w => w.Status == status);
            if (dateFrom.HasValue)
                query = query.Where(w => w.CreatedAt >= dateFrom.Value);
            if (dateTo.HasValue)
            {
                var toEnd = dateTo.Value.Date.AddDays(1);
                query = query.Where(w => w.CreatedAt < toEnd);
            }
        }

        var items = await query.OrderByDescending(w => w.Id).ToListAsync();

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("派工單");

        var headers = new[] { "單號", "客戶代碼", "標題", "描述", "狀態", "預計到場", "預計天數", "處理備註" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        int row = 2;
        foreach (var item in items)
        {
            ws.Cell(row, 1).Value = item.OrderNumber;
            ws.Cell(row, 2).Value = item.Partner?.Code;
            ws.Cell(row, 3).Value = item.Title;
            ws.Cell(row, 4).Value = item.Description;
            ws.Cell(row, 5).Value = item.Status;
            ws.Cell(row, 6).Value = item.ScheduledAt?.ToString("yyyy-MM-dd HH:mm");
            ws.Cell(row, 7).Value = item.ExpectedDays;
            ws.Cell(row, 8).Value = item.Note;
            row++;
        }

        var headerRange = ws.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"派工單_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    public IActionResult DownloadSample()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("派工單");

        var headers = new[] { "單號", "客戶代碼", "標題", "描述", "狀態", "預計到場", "預計天數", "處理備註" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        ws.Cell(2, 1).Value = "WO-202604-001";
        ws.Cell(2, 2).Value = "202604001";
        ws.Cell(2, 3).Value = "範例派工";
        ws.Cell(2, 4).Value = "列印模糊";
        ws.Cell(2, 5).Value = "open";
        ws.Cell(2, 6).Value = "2026-04-30 10:00";
        ws.Cell(2, 7).Value = 3;
        ws.Cell(2, 8).Value = "";

        var headerRange = ws.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "派工單範本.xlsx");
    }

    public IActionResult Import()
    {
        return View();
    }

    #endregion

    private async Task PopulateSelectLists(
        List<int>? selectedPrinterIds = null,
        List<int>? selectedUserIds = null)
    {
        var partners = await _context.Partners
            .Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
        ViewBag.Partners = new SelectList(partners, "Id", "Name");

        var printers = await _context.Printers
            .Where(p => p.IsActive)
            .Include(p => p.Partner)
            .OrderBy(p => p.Partner!.Name).ThenBy(p => p.Name)
            .ToListAsync();
        ViewBag.AllPrinters = printers;
        ViewBag.SelectedPrinterIds = selectedPrinterIds ?? new List<int>();

        var users = await _context.AppUsers
            .Where(u => u.IsActive).OrderBy(u => u.DisplayName).ToListAsync();
        ViewBag.AllUsers = users;
        ViewBag.SelectedUserIds = selectedUserIds ?? new List<int>();
    }
}

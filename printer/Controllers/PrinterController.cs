using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;

namespace printer.Controllers;

/// <summary>
/// 事務機管理頁面
/// </summary>
public class PrinterController : Controller
{
    private readonly PrinterDbContext _context;

    public PrinterController(PrinterDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? keyword, int? partnerId, bool? isActive, int page = 1)
    {
        var query = _context.Printers
            .Include(p => p.Partner)
            .Include(p => p.Model)
            .ThenInclude(m => m!.Brand)
            .AsQueryable();

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(p =>
                p.Name.Contains(keyword) ||
                p.Code.Contains(keyword) ||
                (p.SerialNumber != null && p.SerialNumber.Contains(keyword)));
        }

        if (partnerId.HasValue)
        {
            query = query.Where(p => p.PartnerId == partnerId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        var pageSize = 20;
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Keyword = keyword;
        ViewBag.PartnerId = partnerId;
        ViewBag.IsActive = isActive;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        ViewBag.Total = total;
        ViewBag.Partners = new SelectList(await _context.Partners.Where(p => p.IsActive).ToListAsync(), "Id", "Name");

        // 使用中設備 & 回傳狀態統計
        var totalPrinters = await _context.Printers.CountAsync(p => p.IsActive);
        var totalInUse = totalPrinters;
        var sevenDaysAgo = DateOnly.FromDateTime(DateTime.Today).AddDays(-7);
        var reportedPrinters = await _context.PrintRecords
            .Where(r => r.Date >= sevenDaysAgo)
            .Select(r => r.PrinterId)
            .Distinct()
            .CountAsync();
        ViewBag.TotalPrinters = totalPrinters;
        ViewBag.TotalInUse = totalInUse;
        ViewBag.ReportedPrinters = reportedPrinters;

        // 合約到期提醒
        var allContractPrinters = await _context.Printers
            .Where(p => p.IsActive && p.ContractEndDate.HasValue)
            .ToListAsync();
        ViewBag.ContractAlertCount = allContractPrinters.Count(p => p.IsContractExpiringSoon || p.IsContractExpired);

        return View(items);
    }

    public async Task<IActionResult> Details(int id)
    {
        var printer = await _context.Printers
            .Include(p => p.Partner)
            .Include(p => p.Model)
            .ThenInclude(m => m!.Brand)
            .Include(p => p.Model)
            .ThenInclude(m => m!.PrinterModelSheetTypes)
            .ThenInclude(mst => mst.SheetType)
            .Include(p => p.Maintainers)
            .ThenInclude(m => m.User)
            .Include(p => p.PrintRecords.OrderByDescending(r => r.Date).Take(30))
            .ThenInclude(r => r.Values)
            .ThenInclude(v => v.SheetType)
            .Include(p => p.AlertRecords.OrderByDescending(a => a.CreatedAt).Take(20))
            .FirstOrDefaultAsync(p => p.Id == id);

        if (printer == null)
            return NotFound();

        // 此事務機要顯示的張數類別：以機型配置為主；若機型沒設定，則用記錄中實際出現過的類型
        var modelSheetTypes = printer.Model?.PrinterModelSheetTypes
            .Where(mst => mst.SheetType != null)
            .OrderBy(mst => mst.SortOrder).ThenBy(mst => mst.SheetType!.SortOrder).ThenBy(mst => mst.SheetTypeId)
            .Select(mst => mst.SheetType!)
            .ToList() ?? new List<SheetType>();
        if (modelSheetTypes.Count == 0)
        {
            modelSheetTypes = printer.PrintRecords
                .SelectMany(r => r.Values)
                .Where(v => v.SheetType != null)
                .GroupBy(v => v.SheetTypeId)
                .Select(g => g.First().SheetType!)
                .OrderBy(st => st.SortOrder).ThenBy(st => st.Id)
                .ToList();
        }
        ViewBag.RecordSheetTypes = modelSheetTypes;

        // 計費模組是否啟用
        var billingModule = await _context.SystemModules
            .FirstOrDefaultAsync(m => m.Code == "billing");
        ViewBag.BillingEnabled = billingModule?.IsEnabled == true;

        // 載入計費設定
        if (ViewBag.BillingEnabled)
        {
            ViewBag.BillingConfig = await _context.PrinterBillingConfigs
                .Include(c => c.Tiers)
                .FirstOrDefaultAsync(c => c.PrinterId == id);

            // 張數類型單價與階梯
            ViewBag.SheetPrices = await _context.PrinterBillingSheetPrices
                .Where(p => p.PrinterId == id)
                .Include(p => p.SheetType)
                .OrderBy(p => p.SheetType!.SortOrder).ThenBy(p => p.SheetTypeId)
                .ToListAsync();

            ViewBag.SheetTiers = await _context.PrinterBillingSheetTiers
                .Where(t => t.PrinterId == id)
                .OrderBy(t => t.SheetTypeId).ThenBy(t => t.TierOrder)
                .ToListAsync();
        }

        // 載入可選的維護人員
        ViewBag.AvailableUsers = await _context.AppUsers
            .Where(u => u.IsActive)
            .OrderBy(u => u.DisplayName)
            .ToListAsync();

        // 此設備的帳單記錄
        if (ViewBag.BillingEnabled)
        {
            var printerInvoices = await _context.InvoiceItems
                .Where(item => item.PrinterId == id)
                .Include(item => item.Invoice)
                .ThenInclude(inv => inv!.Partner)
                .Select(item => item.Invoice!)
                .Distinct()
                .OrderByDescending(inv => inv.CreatedAt)
                .Take(20)
                .ToListAsync();
            ViewBag.PrinterInvoices = printerInvoices;
        }

        await LoadViewBagData();
        return View(printer);
    }

    public async Task<IActionResult> Create()
    {
        var now = DateTime.Now;
        var prefix = now.ToString("yyyyMM");
        var maxCode = await _context.Printers
            .Where(p => p.Code.StartsWith(prefix))
            .OrderByDescending(p => p.Code)
            .Select(p => p.Code)
            .FirstOrDefaultAsync();

        var seq = 1;
        if (maxCode != null && maxCode.Length > prefix.Length)
        {
            int.TryParse(maxCode.Substring(prefix.Length), out seq);
            seq++;
        }

        await LoadViewBagData();
        return View(new Printer { Code = $"{prefix}{seq:D3}" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Printer printer)
    {
        if (ModelState.IsValid)
        {
            printer.CreatedAt = DateTime.UtcNow;
            printer.UpdatedAt = DateTime.UtcNow;
            _context.Printers.Add(printer);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        await LoadViewBagData();
        return View(printer);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var printer = await _context.Printers.FindAsync(id);
        if (printer == null)
            return NotFound();

        await LoadViewBagData();
        return View(printer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Printer printer, string? returnTo)
    {
        if (id != printer.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            var existing = await _context.Printers.FindAsync(id);
            if (existing == null)
                return NotFound();

            existing.Code = printer.Code;
            existing.Name = printer.Name;
            existing.PartnerId = printer.PartnerId;
            existing.PartnerNumber = printer.PartnerNumber;
            existing.ModelId = printer.ModelId;
            existing.Description = printer.Description;
            existing.PrinterName = printer.PrinterName;
            existing.SerialNumber = printer.SerialNumber;
            existing.Ip = printer.Ip;
            existing.Mac = printer.Mac;
            existing.IsActive = printer.IsActive;
            existing.UserId = printer.UserId;
            existing.TonerBlackAlertThreshold = printer.TonerBlackAlertThreshold;
            existing.TonerCyanAlertThreshold = printer.TonerCyanAlertThreshold;
            existing.TonerMagentaAlertThreshold = printer.TonerMagentaAlertThreshold;
            existing.TonerYellowAlertThreshold = printer.TonerYellowAlertThreshold;
            existing.DrumBlackAlertThreshold = printer.DrumBlackAlertThreshold;
            existing.DrumCyanAlertThreshold = printer.DrumCyanAlertThreshold;
            existing.DrumMagentaAlertThreshold = printer.DrumMagentaAlertThreshold;
            existing.DrumYellowAlertThreshold = printer.DrumYellowAlertThreshold;
            existing.BrandId = printer.BrandId;
            existing.ContractStartDate = printer.ContractStartDate;
            existing.ContractEndDate = printer.ContractEndDate;
            existing.Deposit = printer.Deposit;
            existing.ContractAlertDays = printer.ContractAlertDays;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            if (returnTo == "Details")
                return RedirectToAction(nameof(Details), new { id });

            return RedirectToAction(nameof(Index));
        }
        await LoadViewBagData();
        return View(printer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var printer = await _context.Printers.FindAsync(id);
        if (printer != null)
        {
            _context.Printers.Remove(printer);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var printer = await _context.Printers.FindAsync(id);
        if (printer == null)
            return NotFound();

        printer.IsActive = !printer.IsActive;
        printer.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Json(new { success = true, isActive = printer.IsActive });
    }

    #region Excel 匯出

    public async Task<IActionResult> Export([FromQuery] int[]? ids, string? keyword, int? partnerId, bool? isActive)
    {
        var query = _context.Printers
            .Include(p => p.Partner)
            .Include(p => p.Model)
            .ThenInclude(m => m!.Brand)
            .AsQueryable();

        if (ids != null && ids.Length > 0)
        {
            query = query.Where(p => ids.Contains(p.Id));
        }
        else
        {
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(p =>
                    p.Name.Contains(keyword) ||
                    p.Code.Contains(keyword) ||
                    (p.SerialNumber != null && p.SerialNumber.Contains(keyword)));
            }

            if (partnerId.HasValue)
            {
                query = query.Where(p => p.PartnerId == partnerId.Value);
            }

            if (isActive.HasValue)
            {
                query = query.Where(p => p.IsActive == isActive.Value);
            }
        }

        var items = await query
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("事務機資料");

        var headers = new[] { "代碼", "名稱", "客戶編號", "廠牌", "型號", "序號", "IP", "MAC", "印表機名稱", "合約開始日期", "合約結束日期", "押金", "啟用" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        int row = 2;
        foreach (var item in items)
        {
            ws.Cell(row, 1).Value = item.Code;
            ws.Cell(row, 2).Value = item.Name;
            ws.Cell(row, 3).Value = item.Partner?.Code;
            ws.Cell(row, 4).Value = item.Model?.Brand?.Name;
            ws.Cell(row, 5).Value = item.Model?.Name;
            ws.Cell(row, 6).Value = item.SerialNumber;
            ws.Cell(row, 7).Value = item.Ip;
            ws.Cell(row, 8).Value = item.Mac;
            ws.Cell(row, 9).Value = item.PrinterName;
            ws.Cell(row, 10).Value = item.ContractStartDate?.ToString("yyyy-MM-dd");
            ws.Cell(row, 11).Value = item.ContractEndDate?.ToString("yyyy-MM-dd");
            ws.Cell(row, 12).Value = item.Deposit ?? 0;
            ws.Cell(row, 13).Value = item.IsActive ? "啟用" : "停用";
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
            $"事務機資料_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    #endregion

    #region Excel 匯入

    public IActionResult Import()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "請選擇檔案";
            return View();
        }

        try
        {
            using var stream = file.OpenReadStream();
            using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
            var worksheet = workbook.Worksheets.First();

            // 讀取標題列
            var headers = new Dictionary<string, int>();
            var headerRow = worksheet.Row(1);
            for (int col = 1; col <= worksheet.ColumnsUsed().Count(); col++)
            {
                var header = headerRow.Cell(col).GetString().Trim();
                if (!string.IsNullOrEmpty(header))
                    headers[header] = col;
            }

            var success = 0;
            var failed = 0;
            var errors = new List<string>();
            var now = DateTime.Now;
            var prefix = now.ToString("yyyyMM");

            // 取得當月最大流水號
            var maxCode = await _context.Printers
                .Where(p => p.Code.StartsWith(prefix))
                .OrderByDescending(p => p.Code)
                .Select(p => p.Code)
                .FirstOrDefaultAsync();

            var seq = 1;
            if (maxCode != null && maxCode.Length > prefix.Length)
            {
                int.TryParse(maxCode.Substring(prefix.Length), out seq);
                seq++;
            }

            for (int row = 2; row <= worksheet.RowsUsed().Count(); row++)
            {
                try
                {
                    var name = GetCellValue(worksheet, row, headers, "名稱");
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        errors.Add($"第 {row} 列: 名稱為空，已跳過");
                        failed++;
                        continue;
                    }

                    // 查詢客戶
                    int? partnerId = null;
                    var partnerCode = GetCellValue(worksheet, row, headers, "客戶編號");
                    if (!string.IsNullOrWhiteSpace(partnerCode))
                    {
                        var partner = await _context.Partners
                            .FirstOrDefaultAsync(p => p.Code == partnerCode || p.Name == partnerCode);
                        if (partner != null)
                            partnerId = partner.Id;
                    }

                    // 代碼：若未提供則自動產生
                    var code = GetCellValue(worksheet, row, headers, "代碼");
                    if (string.IsNullOrWhiteSpace(code))
                    {
                        code = $"{prefix}{seq:D3}";
                        seq++;
                    }

                    // 解析日期
                    DateOnly? contractStart = null;
                    DateOnly? contractEnd = null;
                    var startStr = GetCellValue(worksheet, row, headers, "合約開始日期");
                    var endStr = GetCellValue(worksheet, row, headers, "合約結束日期");
                    if (!string.IsNullOrWhiteSpace(startStr) && DateTime.TryParse(startStr, out var csDate))
                        contractStart = DateOnly.FromDateTime(csDate);
                    if (!string.IsNullOrWhiteSpace(endStr) && DateTime.TryParse(endStr, out var ceDate))
                        contractEnd = DateOnly.FromDateTime(ceDate);

                    // 解析押金
                    decimal? deposit = null;
                    var depositStr = GetCellValue(worksheet, row, headers, "押金");
                    if (!string.IsNullOrWhiteSpace(depositStr) && decimal.TryParse(depositStr, out var depVal))
                        deposit = depVal;

                    var printer = new Printer
                    {
                        Code = code,
                        Name = name,
                        PartnerId = partnerId,
                        SerialNumber = GetCellValue(worksheet, row, headers, "序號"),
                        Ip = GetCellValue(worksheet, row, headers, "IP"),
                        Mac = GetCellValue(worksheet, row, headers, "MAC"),
                        PrinterName = GetCellValue(worksheet, row, headers, "印表機名稱"),
                        ContractStartDate = contractStart,
                        ContractEndDate = contractEnd,
                        Deposit = deposit,
                        Description = GetCellValue(worksheet, row, headers, "說明"),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.Printers.Add(printer);
                    success++;
                }
                catch (Exception ex)
                {
                    errors.Add($"第 {row} 列: {ex.Message}");
                    failed++;
                }
            }

            await _context.SaveChangesAsync();

            var msg = $"匯入完成：成功 {success} 筆";
            if (failed > 0) msg += $"，失敗 {failed} 筆";
            TempData["Success"] = msg;

            if (errors.Any())
                TempData["Error"] = string.Join("\n", errors);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"檔案讀取失敗: {ex.Message}";
        }

        return View();
    }

    public IActionResult DownloadSample()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("事務機資料");

        var headers = new[] { "代碼", "名稱", "客戶編號", "序號", "IP", "MAC", "印表機名稱", "合約開始日期", "合約結束日期", "押金", "說明" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        // 範例資料
        ws.Cell(2, 1).Value = "";
        ws.Cell(2, 2).Value = "辦公室印表機";
        ws.Cell(2, 3).Value = "202601001";
        ws.Cell(2, 4).Value = "SN12345678";
        ws.Cell(2, 5).Value = "192.168.1.100";
        ws.Cell(2, 6).Value = "AA:BB:CC:DD:EE:FF";
        ws.Cell(2, 7).Value = "HP LaserJet";
        ws.Cell(2, 8).Value = "2026-01-01";
        ws.Cell(2, 9).Value = "2027-01-01";
        ws.Cell(2, 10).Value = "5000";
        ws.Cell(2, 11).Value = "一樓辦公室";

        var headerRange = ws.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "事務機匯入範本.xlsx");
    }

    private static string? GetCellValue(ClosedXML.Excel.IXLWorksheet ws, int row, Dictionary<string, int> headers, string headerName)
    {
        if (!headers.TryGetValue(headerName, out var col)) return null;
        var value = ws.Cell(row, col).GetString().Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    #endregion

    private async Task LoadViewBagData()
    {
        var partners = await _context.Partners.Where(p => p.IsActive).ToListAsync();
        ViewBag.Partners = new SelectList(partners, "Id", "Name");
        ViewBag.PartnerNumbers = partners.ToDictionary(p => p.Id.ToString(), p => p.PartnerNumber ?? "");

        var brands = await _context.Brands
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Name).ToListAsync();
        ViewBag.Brands = new SelectList(brands, "Id", "Name");

        var models = await _context.PrinterModels
            .Where(m => m.State == "1")
            .OrderBy(m => m.Name)
            .Select(m => new { id = m.Id, name = m.Name, brandId = m.BrandId })
            .ToListAsync();
        ViewBag.ModelsJson = System.Text.Json.JsonSerializer.Serialize(models);
    }

    #region 維護人員

    [HttpPost]
    public async Task<IActionResult> ReorderMaintainers([FromBody] List<int> orderedIds)
    {
        var maintainers = await _context.PrinterMaintainers
            .Where(m => orderedIds.Contains(m.Id))
            .ToListAsync();
        for (int i = 0; i < orderedIds.Count; i++)
        {
            var m = maintainers.FirstOrDefault(x => x.Id == orderedIds[i]);
            if (m != null) m.SortOrder = i * 10;
        }
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMaintainer(int printerId, int userId, string role)
    {
        if (!await _context.PrinterMaintainers.AnyAsync(m => m.PrinterId == printerId && m.UserId == userId))
        {
            _context.PrinterMaintainers.Add(new Data.Entities.PrinterMaintainer
            {
                PrinterId = printerId,
                UserId = userId,
                Role = role
            });
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Details), new { id = printerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMaintainer(int id, int printerId)
    {
        var maintainer = await _context.PrinterMaintainers.FindAsync(id);
        if (maintainer != null)
        {
            _context.PrinterMaintainers.Remove(maintainer);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Details), new { id = printerId });
    }

    #endregion
}

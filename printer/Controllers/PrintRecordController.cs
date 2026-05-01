using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;

namespace printer.Controllers;

/// <summary>
/// 抄表記錄管理頁面
/// </summary>
public class PrintRecordController : Controller
{
    private readonly PrinterDbContext _context;

    public PrintRecordController(PrinterDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(
        int? printerId,
        int? partnerId,
        DateOnly? startDate,
        DateOnly? endDate,
        int page = 1)
    {
        // 預設帶出今日
        var today = DateOnly.FromDateTime(DateTime.Today);
        startDate ??= today;
        endDate ??= today;

        var query = _context.PrintRecords
            .Include(r => r.Printer)
            .Include(r => r.Partner)
            .Include(r => r.Values)
            .ThenInclude(v => v.SheetType)
            .AsQueryable();

        if (printerId.HasValue)
        {
            query = query.Where(r => r.PrinterId == printerId.Value);
        }

        if (partnerId.HasValue)
        {
            query = query.Where(r => r.PartnerId == partnerId.Value);
        }

        query = query.Where(r => r.Date >= startDate.Value);
        query = query.Where(r => r.Date <= endDate.Value);

        var pageSize = 30;
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // 此頁要顯示的張數類別欄位：若有 printer 篩選用該機型；否則用本頁記錄中出現過的類別
        List<SheetType> indexSheetTypes;
        if (printerId.HasValue)
        {
            var printer = await _context.Printers
                .Include(p => p.Model)
                .ThenInclude(m => m!.PrinterModelSheetTypes)
                .ThenInclude(mst => mst.SheetType)
                .FirstOrDefaultAsync(p => p.Id == printerId.Value);
            indexSheetTypes = printer?.Model?.PrinterModelSheetTypes
                .Where(mst => mst.SheetType != null && mst.SheetType.IsActive)
                .OrderBy(mst => mst.SortOrder).ThenBy(mst => mst.SheetType!.SortOrder).ThenBy(mst => mst.SheetTypeId)
                .Select(mst => mst.SheetType!)
                .ToList() ?? new();
        }
        else
        {
            indexSheetTypes = items
                .SelectMany(r => r.Values)
                .Where(v => v.SheetType != null)
                .GroupBy(v => v.SheetTypeId)
                .Select(g => g.First().SheetType!)
                .OrderBy(st => st.SortOrder).ThenBy(st => st.Id)
                .ToList();
        }
        ViewBag.IndexSheetTypes = indexSheetTypes;

        // 統計資料
        var stats = await query.GroupBy(r => 1).Select(g => new
        {
            TotalBlack = g.Sum(r => r.BlackSheets),
            TotalColor = g.Sum(r => r.ColorSheets),
            TotalLarge = g.Sum(r => r.LargeSheets)
        }).FirstOrDefaultAsync();

        ViewBag.PrinterId = printerId;
        ViewBag.PartnerId = partnerId;
        ViewBag.StartDate = startDate;
        ViewBag.EndDate = endDate;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        ViewBag.Total = total;
        ViewBag.TotalBlack = stats?.TotalBlack ?? 0;
        ViewBag.TotalColor = stats?.TotalColor ?? 0;
        ViewBag.TotalLarge = stats?.TotalLarge ?? 0;
        ViewBag.Partners = new SelectList(
            await _context.Partners.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name");
        ViewBag.Printers = new SelectList(
            await _context.Printers.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name");

        return View(items);
    }

    public async Task<IActionResult> Details(int id)
    {
        var record = await _context.PrintRecords
            .Include(r => r.Printer)
            .ThenInclude(p => p!.Model)
            .ThenInclude(m => m!.Brand)
            .Include(r => r.Partner)
            .Include(r => r.Values)
            .ThenInclude(v => v.SheetType)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (record == null)
            return NotFound();

        await LoadViewBagData();

        // 既有 PrintRecordValues（若無則由 legacy Black/Color/Large 推回）給 edit-mode 動態列用
        var existingValues = record.Values
            .Where(v => v.SheetType != null)
            .OrderBy(v => v.SortOrder).ThenBy(v => v.SheetType!.SortOrder).ThenBy(v => v.SheetTypeId)
            .Select(v => new { id = v.SheetTypeId, name = v.SheetType!.Name, value = v.Value })
            .ToList<object>();

        if (existingValues.Count == 0 && (record.BlackSheets > 0 || record.ColorSheets > 0 || record.LargeSheets > 0))
        {
            var standard = await _context.SheetTypes
                .Where(st => st.IsActive && (st.Name == "黑白" || st.Name == "彩色" || st.Name == "大張"))
                .ToDictionaryAsync(st => st.Name, st => st);
            void Add(string name, int v)
            {
                if (v <= 0 || !standard.TryGetValue(name, out var st)) return;
                existingValues.Add(new { id = st.Id, name = st.Name, value = v });
            }
            Add("黑白", record.BlackSheets);
            Add("彩色", record.ColorSheets);
            Add("大張", record.LargeSheets);
        }

        ViewBag.ExistingValuesJson = System.Text.Json.JsonSerializer.Serialize(existingValues);
        return View(record);
    }

    public async Task<IActionResult> Create()
    {
        await LoadViewBagData();
        return View(new PrintRecord { Date = DateOnly.FromDateTime(DateTime.Today) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PrintRecord record, [FromForm(Name = "sheetValues")] Dictionary<int, int>? sheetValues)
    {
        if (ModelState.IsValid)
        {
            var printer = await _context.Printers.FindAsync(record.PrinterId);
            if (printer != null)
                record.PartnerId = printer.PartnerId;

            // 由動態類別值 backfill 舊欄位（黑白/彩色/大張）讓既有 Index/Export/統計仍可用
            await ApplySheetTypeValuesToLegacyFieldsAsync(record, sheetValues);

            record.CreatedAt = DateTime.UtcNow;
            record.UpdatedAt = DateTime.UtcNow;
            _context.PrintRecords.Add(record);
            await _context.SaveChangesAsync();
            await ReplacePrintRecordValuesAsync(record.Id, sheetValues);
            return RedirectToAction(nameof(Index));
        }
        await LoadViewBagData();
        return View(record);
    }

    /// <summary>
    /// 取得指定事務機要顯示的張數類別清單（新增/編輯抄表記錄頁前端 AJAX 用）。
    /// 邏輯：先以該機型 PrinterModelSheetType 配置；若機型沒設定則回傳全部 active SheetType。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPrinterSheetTypes(int printerId)
    {
        var printer = await _context.Printers
            .Include(p => p.Model)
            .ThenInclude(m => m!.PrinterModelSheetTypes)
            .ThenInclude(mst => mst.SheetType)
            .FirstOrDefaultAsync(p => p.Id == printerId);

        if (printer == null) return NotFound();

        var modelTypes = printer.Model?.PrinterModelSheetTypes
            .Where(mst => mst.SheetType != null && mst.SheetType.IsActive)
            .OrderBy(mst => mst.SortOrder).ThenBy(mst => mst.SheetType!.SortOrder).ThenBy(mst => mst.SheetTypeId)
            .Select(mst => new { id = mst.SheetTypeId, name = mst.SheetType!.Name })
            .ToList() ?? new();

        if (modelTypes.Count == 0)
        {
            modelTypes = await _context.SheetTypes
                .Where(st => st.IsActive)
                .OrderBy(st => st.SortOrder).ThenBy(st => st.Id)
                .Select(st => new { id = st.Id, name = st.Name })
                .ToListAsync();
        }

        return Json(modelTypes);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var record = await _context.PrintRecords
            .Include(r => r.Values)
            .ThenInclude(v => v.SheetType)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (record == null)
            return NotFound();

        await LoadViewBagData();

        // 既有 PrintRecordValues 給前端做為初始列；若沒有，用 legacy Black/Color/Large 名稱推回
        var existingValues = record.Values
            .Where(v => v.SheetType != null)
            .OrderBy(v => v.SheetType!.SortOrder).ThenBy(v => v.SheetTypeId)
            .Select(v => new { id = v.SheetTypeId, name = v.SheetType!.Name, value = v.Value })
            .ToList<object>();

        if (existingValues.Count == 0 && (record.BlackSheets > 0 || record.ColorSheets > 0 || record.LargeSheets > 0))
        {
            var standard = await _context.SheetTypes
                .Where(st => st.IsActive && (st.Name == "黑白" || st.Name == "彩色" || st.Name == "大張"))
                .ToDictionaryAsync(st => st.Name, st => st);
            void Add(string name, int v)
            {
                if (v <= 0 || !standard.TryGetValue(name, out var st)) return;
                existingValues.Add(new { id = st.Id, name = st.Name, value = v });
            }
            Add("黑白", record.BlackSheets);
            Add("彩色", record.ColorSheets);
            Add("大張", record.LargeSheets);
        }

        ViewBag.ExistingValuesJson = System.Text.Json.JsonSerializer.Serialize(existingValues);
        return View(record);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PrintRecord record, [FromForm(Name = "sheetValues")] Dictionary<int, int>? sheetValues, string? returnTo)
    {
        if (id != record.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            var existing = await _context.PrintRecords.FindAsync(id);
            if (existing == null)
                return NotFound();

            existing.PrinterId = record.PrinterId;
            existing.Date = record.Date;
            existing.State = record.State;
            existing.UpdatedAt = DateTime.UtcNow;

            // 由動態類別值 backfill 舊欄位
            await ApplySheetTypeValuesToLegacyFieldsAsync(existing, sheetValues);

            var printer = await _context.Printers.FindAsync(record.PrinterId);
            if (printer != null)
                existing.PartnerId = printer.PartnerId;

            await _context.SaveChangesAsync();
            await ReplacePrintRecordValuesAsync(existing.Id, sheetValues);

            if (returnTo == "Details")
                return RedirectToAction(nameof(Details), new { id });

            return RedirectToAction(nameof(Index));
        }
        await LoadViewBagData();
        return View(record);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var record = await _context.PrintRecords.FindAsync(id);
        if (record != null)
        {
            _context.PrintRecords.Remove(record);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    #region Excel 匯出

    public async Task<IActionResult> Export([FromQuery] int[]? ids, int? printerId, int? partnerId, DateOnly? startDate, DateOnly? endDate)
    {
        var query = _context.PrintRecords
            .Include(r => r.Printer)
            .Include(r => r.Partner)
            .AsQueryable();

        if (ids != null && ids.Length > 0)
        {
            query = query.Where(r => ids.Contains(r.Id));
        }
        else
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            startDate ??= today;
            endDate ??= today;

            if (printerId.HasValue)
            {
                query = query.Where(r => r.PrinterId == printerId.Value);
            }

            if (partnerId.HasValue)
            {
                query = query.Where(r => r.PartnerId == partnerId.Value);
            }

            query = query.Where(r => r.Date >= startDate.Value);
            query = query.Where(r => r.Date <= endDate.Value);
        }

        var items = await query
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Id)
            .ToListAsync();

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("抄表記錄");

        var headers = new[] { "日期", "客戶", "事務機代碼", "事務機名稱", "黑白張數", "彩色張數", "大張張數", "合計", "狀態" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        int row = 2;
        foreach (var item in items)
        {
            ws.Cell(row, 1).Value = item.Date.ToString("yyyy-MM-dd");
            ws.Cell(row, 2).Value = item.Partner?.Name;
            ws.Cell(row, 3).Value = item.Printer?.Code;
            ws.Cell(row, 4).Value = item.Printer?.Name;
            ws.Cell(row, 5).Value = item.BlackSheets;
            ws.Cell(row, 6).Value = item.ColorSheets;
            ws.Cell(row, 7).Value = item.LargeSheets;
            ws.Cell(row, 8).Value = item.BlackSheets + item.ColorSheets + item.LargeSheets;
            ws.Cell(row, 9).Value = item.State == "auto" ? "自動" : "手動";
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
            $"抄表記錄_{DateTime.Now:yyyyMMdd}.xlsx");
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

            for (int row = 2; row <= worksheet.RowsUsed().Count(); row++)
            {
                try
                {
                    // 查詢事務機
                    var printerCode = GetCellValue(worksheet, row, headers, "事務機代碼");
                    if (string.IsNullOrWhiteSpace(printerCode))
                    {
                        errors.Add($"第 {row} 列: 事務機代碼為空，已跳過");
                        failed++;
                        continue;
                    }

                    var printer = await _context.Printers
                        .FirstOrDefaultAsync(p => p.Code == printerCode);
                    if (printer == null)
                    {
                        errors.Add($"第 {row} 列: 找不到事務機代碼 {printerCode}，已跳過");
                        failed++;
                        continue;
                    }

                    // 解析日期
                    var dateStr = GetCellValue(worksheet, row, headers, "日期");
                    DateOnly date;
                    if (!string.IsNullOrWhiteSpace(dateStr) && DateTime.TryParse(dateStr, out var parsedDate))
                    {
                        date = DateOnly.FromDateTime(parsedDate);
                    }
                    else
                    {
                        date = DateOnly.FromDateTime(DateTime.Today);
                    }

                    // 解析張數
                    int.TryParse(GetCellValue(worksheet, row, headers, "黑白張數") ?? "0", out var blackSheets);
                    int.TryParse(GetCellValue(worksheet, row, headers, "彩色張數") ?? "0", out var colorSheets);
                    int.TryParse(GetCellValue(worksheet, row, headers, "大張張數") ?? "0", out var largeSheets);

                    var record = new PrintRecord
                    {
                        PrinterId = printer.Id,
                        PartnerId = printer.PartnerId,
                        Date = date,
                        BlackSheets = blackSheets,
                        ColorSheets = colorSheets,
                        LargeSheets = largeSheets,
                        State = "manual",
                        Count = 1,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.PrintRecords.Add(record);
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
        var ws = workbook.Worksheets.Add("抄表記錄");

        var headers = new[] { "日期", "事務機代碼", "黑白張數", "彩色張數", "大張張數" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        // 範例資料
        ws.Cell(2, 1).Value = "2026-03-20";
        ws.Cell(2, 2).Value = "202601001";
        ws.Cell(2, 3).Value = "1500";
        ws.Cell(2, 4).Value = "300";
        ws.Cell(2, 5).Value = "50";

        var headerRange = ws.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "抄表記錄匯入範本.xlsx");
    }

    private static string? GetCellValue(ClosedXML.Excel.IXLWorksheet ws, int row, Dictionary<string, int> headers, string headerName)
    {
        if (!headers.TryGetValue(headerName, out var col)) return null;
        var value = ws.Cell(row, col).GetString().Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    #endregion

    /// <summary>
    /// 依客戶統計
    /// </summary>
    public async Task<IActionResult> ByPartner(DateOnly? startDate, DateOnly? endDate)
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.Today.AddMonths(-1));
        var end = endDate ?? DateOnly.FromDateTime(DateTime.Today);

        var stats = await _context.PrintRecords
            .Where(r => r.Date >= start && r.Date <= end && r.PartnerId != null)
            .GroupBy(r => new { r.PartnerId, r.Partner!.Name, r.Partner.Code })
            .Select(g => new PartnerStatDto
            {
                PartnerId = g.Key.PartnerId!.Value,
                PartnerName = g.Key.Name,
                PartnerCode = g.Key.Code,
                RecordCount = g.Count(),
                TotalBlack = g.Sum(r => r.BlackSheets),
                TotalColor = g.Sum(r => r.ColorSheets),
                TotalLarge = g.Sum(r => r.LargeSheets)
            })
            .OrderByDescending(s => s.TotalBlack + s.TotalColor + s.TotalLarge)
            .ToListAsync();

        ViewBag.StartDate = start;
        ViewBag.EndDate = end;

        return View(stats);
    }

    /// <summary>
    /// 依事務機統計
    /// </summary>
    public async Task<IActionResult> ByPrinter(int? partnerId, DateOnly? startDate, DateOnly? endDate)
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.Today.AddMonths(-1));
        var end = endDate ?? DateOnly.FromDateTime(DateTime.Today);

        var query = _context.PrintRecords
            .Include(r => r.Printer)
            .ThenInclude(p => p!.Partner)
            .Where(r => r.Date >= start && r.Date <= end);

        if (partnerId.HasValue)
        {
            query = query.Where(r => r.PartnerId == partnerId.Value);
        }

        var stats = await query
            .GroupBy(r => new { r.PrinterId, r.Printer!.Code, r.Printer.Name, PartnerName = r.Printer.Partner!.Name })
            .Select(g => new PrinterStatDto
            {
                PrinterId = g.Key.PrinterId,
                PrinterCode = g.Key.Code,
                PrinterName = g.Key.Name,
                PartnerName = g.Key.PartnerName,
                RecordCount = g.Count(),
                TotalBlack = g.Sum(r => r.BlackSheets),
                TotalColor = g.Sum(r => r.ColorSheets),
                TotalLarge = g.Sum(r => r.LargeSheets)
            })
            .OrderByDescending(s => s.TotalBlack + s.TotalColor + s.TotalLarge)
            .ToListAsync();

        ViewBag.PartnerId = partnerId;
        ViewBag.StartDate = start;
        ViewBag.EndDate = end;
        ViewBag.Partners = new SelectList(
            await _context.Partners.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name");

        return View(stats);
    }

    /// <summary>
    /// 依表單傳入的 sheet_type_id → value 字典寫回 PrintRecordValues（先清舊、再寫新；value=0 不寫）。
    /// SortOrder 用 dict 迭代序設定，保留使用者在表單中的順序。
    /// </summary>
    private async Task ReplacePrintRecordValuesAsync(int recordId, Dictionary<int, int>? values)
    {
        var existing = await _context.PrintRecordValues
            .Where(v => v.RecordId == recordId).ToListAsync();
        _context.PrintRecordValues.RemoveRange(existing);

        if (values != null)
        {
            int order = 0;
            foreach (var (sheetTypeId, value) in values)
            {
                if (value <= 0) continue;
                _context.PrintRecordValues.Add(new PrintRecordValue
                {
                    RecordId = recordId,
                    SheetTypeId = sheetTypeId,
                    Value = value,
                    SortOrder = ++order
                });
            }
        }
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// view-mode 拖曳排序：依傳入順序寫回每筆 PrintRecordValue 的 SortOrder（從 1 起算）。
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReorderRecordValues(int recordId, [FromForm] int[] sheetTypeIds)
    {
        var values = await _context.PrintRecordValues
            .Where(v => v.RecordId == recordId && sheetTypeIds.Contains(v.SheetTypeId))
            .ToListAsync();
        var indexMap = sheetTypeIds.Select((id, i) => new { id, i })
            .ToDictionary(x => x.id, x => x.i + 1);
        foreach (var v in values)
        {
            if (indexMap.TryGetValue(v.SheetTypeId, out var order))
                v.SortOrder = order;
        }
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    /// <summary>
    /// 由動態類別值反推舊 BlackSheets/ColorSheets/LargeSheets 欄位（依 SheetType.Name 對應），
    /// 讓既有 Index/Excel/統計頁不需改寫；對應不到名稱的類別只記在 PrintRecordValues。
    /// </summary>
    private async Task ApplySheetTypeValuesToLegacyFieldsAsync(PrintRecord record, Dictionary<int, int>? values)
    {
        record.BlackSheets = 0;
        record.ColorSheets = 0;
        record.LargeSheets = 0;
        if (values == null || values.Count == 0) return;

        var ids = values.Keys.ToList();
        var nameById = await _context.SheetTypes
            .Where(st => ids.Contains(st.Id))
            .ToDictionaryAsync(st => st.Id, st => st.Name);

        foreach (var (sheetTypeId, value) in values)
        {
            if (value <= 0) continue;
            if (!nameById.TryGetValue(sheetTypeId, out var name)) continue;
            switch (name)
            {
                case "黑白": record.BlackSheets += value; break;
                case "彩色": record.ColorSheets += value; break;
                case "大張":
                case "彩色大張": record.LargeSheets += value; break;
            }
        }
    }

    private async Task LoadViewBagData(int? partnerId = null)
    {
        ViewBag.Partners = new SelectList(
            await _context.Partners
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .Select(p => new { p.Id, p.Name })
                .ToListAsync(),
            "Id", "Name");

        // 若有指定客戶，載入該客戶的事務機；否則載入全部
        var printerQuery = _context.Printers
            .Include(p => p.Partner)
            .Where(p => p.IsActive);

        if (partnerId.HasValue)
        {
            printerQuery = printerQuery.Where(p => p.PartnerId == partnerId.Value);
        }

        ViewBag.Printers = new SelectList(
            await printerQuery
                .Select(p => new { p.Id, Name = p.Partner!.Name + " - " + p.Name })
                .ToListAsync(),
            "Id", "Name");

        ViewBag.States = new SelectList(new[]
        {
            new { Value = "auto", Text = "自動回傳" },
            new { Value = "manual", Text = "手動輸入" }
        }, "Value", "Text");

        // 提供前端做「新增張數類別」下拉用的全部 active SheetType
        ViewBag.AllSheetTypesJson = System.Text.Json.JsonSerializer.Serialize(
            await _context.SheetTypes
                .Where(st => st.IsActive)
                .OrderBy(st => st.SortOrder).ThenBy(st => st.Id)
                .Select(st => new { id = st.Id, name = st.Name })
                .ToListAsync());
    }
}

public class PartnerStatDto
{
    public int PartnerId { get; set; }
    public string PartnerName { get; set; } = "";
    public string PartnerCode { get; set; } = "";
    public int RecordCount { get; set; }
    public int TotalBlack { get; set; }
    public int TotalColor { get; set; }
    public int TotalLarge { get; set; }
    public int Total => TotalBlack + TotalColor + TotalLarge;
}

public class PrinterStatDto
{
    public int PrinterId { get; set; }
    public string PrinterCode { get; set; } = "";
    public string PrinterName { get; set; } = "";
    public string PartnerName { get; set; } = "";
    public int RecordCount { get; set; }
    public int TotalBlack { get; set; }
    public int TotalColor { get; set; }
    public int TotalLarge { get; set; }
    public int Total => TotalBlack + TotalColor + TotalLarge;
}

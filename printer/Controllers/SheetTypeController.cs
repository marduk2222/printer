using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;

namespace printer.Controllers;

public class SheetTypeController : Controller
{
    private readonly PrinterDbContext _context;

    /// <summary>
    /// driver_key 四維格式：{output}.{category}.{color}.{size}
    /// 每個維度可填字面值或 *（wildcard，代表「任何」），但禁止 *.*.*.* 全 wildcard。
    /// 維度字面值：
    ///   output   = printed | scanned
    ///   category = print | copy | fax | network | list | scan
    ///   color    = black | mono | duotone | color_full
    ///   size     = normal | large
    /// 若 output 與 category 同時為字面值（非 *），仍需符合 printed↔print/copy/fax/network/list、scanned↔scan 對應。
    /// 與 printer_info / printer_setup 送上來的 CounterItem 維度一致；wildcard 由 DriverKeyMatcher 解析。
    /// </summary>
    private static readonly Regex DriverKeyPattern = new Regex(
        @"^(?:printed|scanned|\*)\.(?:print|copy|fax|network|list|scan|\*)\.(?:black|mono|duotone|color_full|\*)\.(?:normal|large|\*)$",
        RegexOptions.Compiled);

    /// <summary>
    /// 在 regex 通過的前提下，額外檢查 output-category 對應合理性（避免 printed.scan / scanned.print 之類非法組合）。
    /// 任一邊為 * 時不檢查（wildcard 由 matcher 處理）。
    /// </summary>
    private static bool IsValidDriverKey(string key)
    {
        if (!DriverKeyPattern.IsMatch(key)) return false;
        if (key == "*.*.*.*") return false; // 全 wildcard 無意義

        var parts = key.Split('.');
        var output = parts[0]; var category = parts[1];
        if (output == "*" || category == "*") return true;
        if (output == "printed") return category is "print" or "copy" or "fax" or "network" or "list";
        if (output == "scanned") return category == "scan";
        return false;
    }

    public SheetTypeController(PrinterDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var sheetTypes = await _context.SheetTypes
            .Include(s => s.Keys)
            .Include(s => s.PrinterModelSheetTypes)
                .ThenInclude(m => m.PrinterModel)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync();

        return View(sheetTypes);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "名稱不可空白";
            return RedirectToAction(nameof(Index));
        }

        _context.SheetTypes.Add(new SheetType
        {
            Name = name.Trim(),
            SortOrder = sortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        TempData["Success"] = $"張數類型「{name}」已建立";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string name, int sortOrder, bool isActive)
    {
        var sheetType = await _context.SheetTypes.FindAsync(id);
        if (sheetType == null) return NotFound();

        sheetType.Name = name.Trim();
        sheetType.SortOrder = sortOrder;
        sheetType.IsActive = isActive;
        await _context.SaveChangesAsync();
        TempData["Success"] = "已更新";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var sheetType = await _context.SheetTypes
            .Include(s => s.PrinterModelSheetTypes)
            .Include(s => s.RecordValues)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sheetType == null) return NotFound();

        if (sheetType.RecordValues.Any())
        {
            TempData["Error"] = "此類型已有抄表資料，無法刪除";
            return RedirectToAction(nameof(Index));
        }

        _context.SheetTypes.Remove(sheetType);
        await _context.SaveChangesAsync();
        TempData["Success"] = "已刪除";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Reorder([FromBody] List<int> orderedIds)
    {
        var sheetTypes = await _context.SheetTypes.ToListAsync();
        for (int i = 0; i < orderedIds.Count; i++)
        {
            var st = sheetTypes.FirstOrDefault(s => s.Id == orderedIds[i]);
            if (st != null)
                st.SortOrder = i * 10;
        }
        await _context.SaveChangesAsync();
        return Ok();
    }

    /// <summary>
    /// 驅動參數管理獨立頁面：列出該 SheetType 的所有 keys（可刪除）+ 新增區塊。
    /// 對應 user 要求「驅動參數有額外的畫面進行修改調整」，從 Index 拆出。
    /// </summary>
    public async Task<IActionResult> Keys(int id)
    {
        var sheetType = await _context.SheetTypes
            .Include(s => s.Keys)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (sheetType == null) return NotFound();
        return View(sheetType);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddKey([FromForm] int sheetTypeId, [FromForm] List<string>? driverKeys, [FromForm] string? returnTo = null)
    {
        var requested = (driverKeys ?? new List<string>())
            .Select(k => (k ?? string.Empty).Trim().ToLowerInvariant())
            .Where(k => !string.IsNullOrEmpty(k))
            .Distinct()
            .ToList();

        IActionResult Back() =>
            string.Equals(returnTo, "keys", StringComparison.OrdinalIgnoreCase)
                ? RedirectToAction(nameof(Keys), new { id = sheetTypeId })
                : RedirectToAction(nameof(Index));

        if (requested.Count == 0)
        {
            TempData["Error"] = "尚未選擇任何 driver_key";
            return Back();
        }

        var invalid = requested.Where(k => !IsValidDriverKey(k)).ToList();
        if (invalid.Count > 0)
        {
            TempData["Error"] = $"以下格式不符（須為 {{output}}.{{category}}.{{color}}.{{size}}，未指定可用 * 代表任意；不可全 *）：{string.Join(", ", invalid)}";
            return Back();
        }

        var existing = await _context.SheetTypeKeys
            .Where(k => k.SheetTypeId == sheetTypeId)
            .Select(k => k.DriverKey)
            .ToListAsync();
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var toInsert = requested.Where(k => !existingSet.Contains(k)).ToList();
        foreach (var k in toInsert)
        {
            _context.SheetTypeKeys.Add(new SheetTypeKey
            {
                SheetTypeId = sheetTypeId,
                DriverKey = k
            });
        }
        await _context.SaveChangesAsync();

        var skipped = requested.Count - toInsert.Count;
        TempData["Success"] = skipped > 0
            ? $"已新增 {toInsert.Count} 筆 driver_key（{skipped} 筆已存在跳過）"
            : $"已新增 {toInsert.Count} 筆 driver_key";
        return Back();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteKey(int id, [FromForm] string? returnTo = null)
    {
        var key = await _context.SheetTypeKeys.FindAsync(id);
        int? sheetTypeId = key?.SheetTypeId;
        if (key != null)
        {
            _context.SheetTypeKeys.Remove(key);
            await _context.SaveChangesAsync();
            TempData["Success"] = "驅動參數已刪除";
        }
        return string.Equals(returnTo, "keys", StringComparison.OrdinalIgnoreCase) && sheetTypeId.HasValue
            ? RedirectToAction(nameof(Keys), new { id = sheetTypeId.Value })
            : RedirectToAction(nameof(Index));
    }

    #region Excel 匯出 / 匯入 / 下載範本

    public async Task<IActionResult> Export([FromQuery] int[]? ids)
    {
        var query = _context.SheetTypes.Include(s => s.Keys).AsQueryable();
        if (ids != null && ids.Length > 0)
            query = query.Where(s => ids.Contains(s.Id));

        var items = await query.OrderBy(s => s.SortOrder).ThenBy(s => s.Name).ToListAsync();

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("張數類型");

        var headers = new[] { "名稱", "驅動參數", "排序" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        int row = 2;
        foreach (var item in items)
        {
            ws.Cell(row, 1).Value = item.Name;
            ws.Cell(row, 2).Value = string.Join(",", item.Keys.Select(k => k.DriverKey));
            ws.Cell(row, 3).Value = item.SortOrder;
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
            $"張數類型_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    public IActionResult DownloadSample()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("張數類型");

        var headers = new[] { "名稱", "驅動參數", "排序" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        // 驅動參數須為四維格式 {output}.{category}.{color}.{size}，未指定維度可用 * 代表任意
        ws.Cell(2, 1).Value = "黑白";
        ws.Cell(2, 2).Value = "printed.print.black.normal,printed.copy.black.normal";
        ws.Cell(2, 3).Value = 10;

        ws.Cell(3, 1).Value = "彩色";
        ws.Cell(3, 2).Value = "printed.*.color_full.normal,printed.*.duotone.normal";
        ws.Cell(3, 3).Value = 20;

        var headerRange = ws.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "張數類型匯入範本.xlsx");
    }

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
                    var name = GetCellValue(worksheet, row, headers, "名稱");
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        errors.Add($"第 {row} 列: 名稱為空，已跳過");
                        failed++;
                        continue;
                    }

                    var driverKeyStr = GetCellValue(worksheet, row, headers, "驅動參數");
                    var sortOrderStr = GetCellValue(worksheet, row, headers, "排序");
                    int sortOrder = 0;
                    int.TryParse(sortOrderStr, out sortOrder);

                    var existing = await _context.SheetTypes
                        .Include(s => s.Keys)
                        .FirstOrDefaultAsync(s => s.Name == name);

                    SheetType target;
                    if (existing != null)
                    {
                        existing.SortOrder = sortOrder;
                        target = existing;
                    }
                    else
                    {
                        target = new SheetType
                        {
                            Name = name,
                            SortOrder = sortOrder,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.SheetTypes.Add(target);
                        await _context.SaveChangesAsync();
                    }

                    if (!string.IsNullOrWhiteSpace(driverKeyStr))
                    {
                        var keys = driverKeyStr.Split(new[] { ',', ';', '，' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(k => k.Trim().ToLowerInvariant())
                            .Where(k => !string.IsNullOrWhiteSpace(k))
                            .Distinct()
                            .ToList();

                        var invalid = keys.Where(k => !IsValidDriverKey(k)).ToList();
                        if (invalid.Count > 0)
                        {
                            errors.Add($"第 {row} 列: 以下 driver_key 格式不符（須為 {{output}}.{{category}}.{{color}}.{{size}}，未指定可用 * 代表任意；不可全 *）: {string.Join(", ", invalid)}");
                        }

                        var validKeys = keys.Where(IsValidDriverKey).ToList();
                        var existingKeys = await _context.SheetTypeKeys
                            .Where(k => k.SheetTypeId == target.Id)
                            .ToListAsync();

                        foreach (var k in validKeys)
                        {
                            if (!existingKeys.Any(e => e.DriverKey == k))
                            {
                                _context.SheetTypeKeys.Add(new SheetTypeKey
                                {
                                    SheetTypeId = target.Id,
                                    DriverKey = k
                                });
                            }
                        }
                    }

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

    private static string? GetCellValue(ClosedXML.Excel.IXLWorksheet ws, int row, Dictionary<string, int> headers, string headerName)
    {
        if (!headers.TryGetValue(headerName, out var col)) return null;
        var value = ws.Cell(row, col).GetString().Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    #endregion
}

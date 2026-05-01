using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;

namespace printer.Controllers;

public class SheetTypeController : Controller
{
    private readonly PrinterDbContext _context;

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddKey(int sheetTypeId, string driverKey)
    {
        driverKey = driverKey.Trim();
        var exists = await _context.SheetTypeKeys
            .AnyAsync(k => k.SheetTypeId == sheetTypeId && k.DriverKey == driverKey);

        if (!exists)
        {
            _context.SheetTypeKeys.Add(new SheetTypeKey
            {
                SheetTypeId = sheetTypeId,
                DriverKey = driverKey
            });
            await _context.SaveChangesAsync();
            TempData["Success"] = "驅動參數已新增";
        }
        else
        {
            TempData["Error"] = "此驅動參數已存在";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteKey(int id)
    {
        var key = await _context.SheetTypeKeys.FindAsync(id);
        if (key != null)
        {
            _context.SheetTypeKeys.Remove(key);
            await _context.SaveChangesAsync();
            TempData["Success"] = "驅動參數已刪除";
        }
        return RedirectToAction(nameof(Index));
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

        ws.Cell(2, 1).Value = "黑白";
        ws.Cell(2, 2).Value = "print_black,copy_black";
        ws.Cell(2, 3).Value = 10;

        ws.Cell(3, 1).Value = "彩色";
        ws.Cell(3, 2).Value = "print_color,copy_color";
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
                            .Select(k => k.Trim())
                            .Where(k => !string.IsNullOrWhiteSpace(k))
                            .Distinct()
                            .ToList();

                        var existingKeys = await _context.SheetTypeKeys
                            .Where(k => k.SheetTypeId == target.Id)
                            .ToListAsync();

                        foreach (var k in keys)
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

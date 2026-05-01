using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;
using printer.Services;

namespace printer.Controllers;

[Authorize(Roles = "admin,supervisor")]
public class SheetTypeConversionController : Controller
{
    private readonly PrinterDbContext _context;
    private readonly IWorkOrderService _workOrderService;

    public SheetTypeConversionController(PrinterDbContext context, IWorkOrderService workOrderService)
    {
        _context = context;
        _workOrderService = workOrderService;
    }

    public async Task<IActionResult> Index()
    {
        var rates = await _workOrderService.GetConversionRatesAsync();
        return View(rates);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateSheetTypeSelect();
        return View(new SheetTypeConversionRate { Ratio = 10, IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SheetTypeConversionRate rate)
    {
        if (rate.FromSheetTypeId == rate.ToSheetTypeId)
            ModelState.AddModelError("", "被折抵類型和折抵來源類型不可相同");

        if (rate.Ratio <= 0)
            ModelState.AddModelError("Ratio", "比例必須大於 0");

        // 確認不重複
        var exists = await _context.SheetTypeConversionRates
            .AnyAsync(r => r.FromSheetTypeId == rate.FromSheetTypeId && r.ToSheetTypeId == rate.ToSheetTypeId);
        if (exists)
            ModelState.AddModelError("", "此組合的互換比例已存在");

        if (!ModelState.IsValid)
        {
            await PopulateSheetTypeSelect();
            return View(rate);
        }

        try
        {
            await _workOrderService.SaveConversionRateAsync(rate);
            TempData["Success"] = "互換比例已儲存";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            await PopulateSheetTypeSelect();
            return View(rate);
        }
    }

    public async Task<IActionResult> Edit(int id)
    {
        var rate = await _workOrderService.GetConversionRateAsync(id);
        if (rate == null) return NotFound();
        await PopulateSheetTypeSelect();
        return View(rate);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SheetTypeConversionRate rate)
    {
        if (id != rate.Id) return BadRequest();

        if (rate.FromSheetTypeId == rate.ToSheetTypeId)
            ModelState.AddModelError("", "被折抵類型和折抵來源類型不可相同");

        if (rate.Ratio <= 0)
            ModelState.AddModelError("Ratio", "比例必須大於 0");

        // 確認不重複（排除自身）
        var exists = await _context.SheetTypeConversionRates
            .AnyAsync(r => r.Id != id && r.FromSheetTypeId == rate.FromSheetTypeId && r.ToSheetTypeId == rate.ToSheetTypeId);
        if (exists)
            ModelState.AddModelError("", "此組合的互換比例已存在");

        if (!ModelState.IsValid)
        {
            await PopulateSheetTypeSelect();
            return View(rate);
        }

        try
        {
            await _workOrderService.SaveConversionRateAsync(rate);
            TempData["Success"] = "互換比例已更新";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            await PopulateSheetTypeSelect();
            return View(rate);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (await _workOrderService.DeleteConversionRateAsync(id))
            TempData["Success"] = "互換比例已刪除";
        else
            TempData["Error"] = "找不到指定的互換比例";

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateSheetTypeSelect(int? fromId = null, int? toId = null)
    {
        var sheetTypes = await _context.SheetTypes
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

        ViewBag.FromSheetTypes = new SelectList(sheetTypes, "Id", "Name", fromId);
        ViewBag.ToSheetTypes = new SelectList(sheetTypes, "Id", "Name", toId);
    }

    #region Excel 匯出 / 匯入 / 下載範本

    public async Task<IActionResult> Export([FromQuery] int[]? ids)
    {
        var query = _context.SheetTypeConversionRates
            .Include(r => r.FromSheetType)
            .Include(r => r.ToSheetType)
            .AsQueryable();

        if (ids != null && ids.Length > 0)
            query = query.Where(r => ids.Contains(r.Id));

        var items = await query.OrderBy(r => r.Id).ToListAsync();

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("張數互換比例");

        var headers = new[] { "來源類型", "目標類型", "比例", "啟用", "備註" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        int row = 2;
        foreach (var item in items)
        {
            ws.Cell(row, 1).Value = item.FromSheetType?.Name;
            ws.Cell(row, 2).Value = item.ToSheetType?.Name;
            ws.Cell(row, 3).Value = item.Ratio;
            ws.Cell(row, 4).Value = item.IsActive ? "是" : "否";
            ws.Cell(row, 5).Value = item.Note;
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
            $"張數互換比例_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    public IActionResult DownloadSample()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("張數互換比例");

        var headers = new[] { "來源類型", "目標類型", "比例", "啟用", "備註" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        ws.Cell(2, 1).Value = "黑白";
        ws.Cell(2, 2).Value = "彩色";
        ws.Cell(2, 3).Value = 10;
        ws.Cell(2, 4).Value = "是";
        ws.Cell(2, 5).Value = "1 彩色贈送可折抵 10 黑白";

        var headerRange = ws.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "張數互換比例範本.xlsx");
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

            var sheetTypes = await _context.SheetTypes.ToListAsync();

            for (int row = 2; row <= worksheet.RowsUsed().Count(); row++)
            {
                try
                {
                    var fromName = GetCellValue(worksheet, row, headers, "來源類型");
                    var toName = GetCellValue(worksheet, row, headers, "目標類型");
                    var ratioStr = GetCellValue(worksheet, row, headers, "比例");

                    if (string.IsNullOrWhiteSpace(fromName) || string.IsNullOrWhiteSpace(toName))
                    {
                        errors.Add($"第 {row} 列: 來源或目標類型為空，已跳過");
                        failed++;
                        continue;
                    }

                    var fromType = sheetTypes.FirstOrDefault(s => s.Name == fromName);
                    var toType = sheetTypes.FirstOrDefault(s => s.Name == toName);
                    if (fromType == null || toType == null)
                    {
                        errors.Add($"第 {row} 列: 找不到張數類型「{fromName}」或「{toName}」，已跳過");
                        failed++;
                        continue;
                    }

                    if (fromType.Id == toType.Id)
                    {
                        errors.Add($"第 {row} 列: 來源與目標類型相同，已跳過");
                        failed++;
                        continue;
                    }

                    if (!decimal.TryParse(ratioStr, out var ratio) || ratio <= 0)
                    {
                        errors.Add($"第 {row} 列: 比例「{ratioStr}」無效，已跳過");
                        failed++;
                        continue;
                    }

                    var isActiveStr = GetCellValue(worksheet, row, headers, "啟用") ?? "是";
                    var isActive = isActiveStr == "是" || string.Equals(isActiveStr, "true", StringComparison.OrdinalIgnoreCase) || isActiveStr == "1";
                    var note = GetCellValue(worksheet, row, headers, "備註");

                    var existing = await _context.SheetTypeConversionRates
                        .FirstOrDefaultAsync(r => r.FromSheetTypeId == fromType.Id && r.ToSheetTypeId == toType.Id);

                    if (existing != null)
                    {
                        existing.Ratio = ratio;
                        existing.IsActive = isActive;
                        existing.Note = note;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        _context.SheetTypeConversionRates.Add(new SheetTypeConversionRate
                        {
                            FromSheetTypeId = fromType.Id,
                            ToSheetTypeId = toType.Id,
                            Ratio = ratio,
                            IsActive = isActive,
                            Note = note,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
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

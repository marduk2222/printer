using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;

namespace printer.Controllers;

public class PrinterModelController : Controller
{
    private readonly PrinterDbContext _context;
    private readonly IWebHostEnvironment _env;

    public PrinterModelController(PrinterDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    public async Task<IActionResult> Index(int? brandId)
    {
        var query = _context.PrinterModels
            .Include(m => m.Brand)
            .Include(m => m.Printers)
            .Include(m => m.PrinterModelSheetTypes)
            .AsQueryable();

        if (brandId.HasValue)
            query = query.Where(m => m.BrandId == brandId.Value);

        var items = await query.OrderBy(m => m.SortOrder).ThenBy(m => m.Brand!.Name).ThenBy(m => m.Name).ToListAsync();

        ViewBag.BrandId = brandId;
        ViewBag.Brands = new SelectList(
            await _context.Brands.OrderBy(b => b.Name).ToListAsync(), "Id", "Name", brandId);

        return View(items);
    }

    public async Task<IActionResult> Create()
    {
        await LoadBrands();
        return View(new PrinterModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PrinterModel model, IFormFile? image)
    {
        if (ModelState.IsValid)
        {
            model.ImagePath = await SaveImage(image, "models");
            model.CreatedAt = DateTime.UtcNow;
            _context.PrinterModels.Add(model);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        await LoadBrands();
        return View(model);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var model = await _context.PrinterModels
            .Include(m => m.PrinterModelSheetTypes.OrderBy(s => s.SortOrder))
                .ThenInclude(s => s.SheetType)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (model == null) return NotFound();
        await LoadBrands();

        // 尚未加入此機型的張數類型（供選擇新增）
        var existingIds = model.PrinterModelSheetTypes.Select(s => s.SheetTypeId).ToList();
        ViewBag.AvailableSheetTypes = await _context.SheetTypes
            .Where(s => s.IsActive && !existingIds.Contains(s.Id))
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PrinterModel model, IFormFile? image)
    {
        if (id != model.Id) return NotFound();

        if (ModelState.IsValid)
        {
            var existing = await _context.PrinterModels.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Name = model.Name;
            existing.BrandId = model.BrandId;
            existing.Code = model.Code;
            existing.Feature = model.Feature;
            existing.Description = model.Description;
            existing.State = model.State;
            existing.Verify = model.Verify;

            if (image != null)
            {
                DeleteImage(existing.ImagePath);
                existing.ImagePath = await SaveImage(image, "models");
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        await LoadBrands();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var model = await _context.PrinterModels
            .Include(m => m.Printers)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (model == null) return NotFound();

        if (model.Printers.Any())
        {
            TempData["Error"] = "此型號下有事務機，無法刪除";
            return RedirectToAction(nameof(Index));
        }

        DeleteImage(model.ImagePath);
        _context.PrinterModels.Remove(model);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleState(int id)
    {
        var model = await _context.PrinterModels.FindAsync(id);
        if (model == null) return NotFound();

        model.State = model.State == "1" ? "0" : "1";
        await _context.SaveChangesAsync();
        return Json(new { success = true, state = model.State });
    }

    [HttpPost]
    public async Task<IActionResult> Reorder([FromBody] List<int> orderedIds)
    {
        var models = await _context.PrinterModels.ToListAsync();
        for (int i = 0; i < orderedIds.Count; i++)
        {
            var m = models.FirstOrDefault(x => x.Id == orderedIds[i]);
            if (m != null) m.SortOrder = i * 10;
        }
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> ReorderSheetTypes([FromBody] ReorderSheetTypesRequest request)
    {
        var entries = await _context.PrinterModelSheetTypes
            .Where(s => s.PrinterModelId == request.ModelId)
            .ToListAsync();

        for (int i = 0; i < request.OrderedIds.Count; i++)
        {
            var entry = entries.FirstOrDefault(s => s.Id == request.OrderedIds[i]);
            if (entry != null)
                entry.SortOrder = i * 10;
        }
        await _context.SaveChangesAsync();
        return Ok();
    }

    #region 機型張數類型配置

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSheetType(int modelId, int sheetTypeId, int sortOrder)
    {
        var exists = await _context.PrinterModelSheetTypes
            .AnyAsync(s => s.PrinterModelId == modelId && s.SheetTypeId == sheetTypeId);
        if (!exists)
        {
            _context.PrinterModelSheetTypes.Add(new PrinterModelSheetType
            {
                PrinterModelId = modelId,
                SheetTypeId = sheetTypeId,
                SortOrder = sortOrder
            });
            await _context.SaveChangesAsync();
            TempData["Success"] = "張數類型已加入";
        }
        return RedirectToAction(nameof(Edit), new { id = modelId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSheetType(int id, int modelId)
    {
        var entry = await _context.PrinterModelSheetTypes.FindAsync(id);
        if (entry != null)
        {
            _context.PrinterModelSheetTypes.Remove(entry);
            await _context.SaveChangesAsync();
            TempData["Success"] = "張數類型已移除";
        }
        return RedirectToAction(nameof(Edit), new { id = modelId });
    }

    #endregion

    private async Task LoadBrands()
    {
        ViewBag.Brands = new SelectList(
            await _context.Brands.OrderBy(b => b.Name).ToListAsync(), "Id", "Name");
    }

    private async Task<string?> SaveImage(IFormFile? file, string folder)
    {
        if (file == null || file.Length == 0) return null;

        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", folder);
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/uploads/{folder}/{fileName}";
    }

    private void DeleteImage(string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) return;
        var fullPath = Path.Combine(_env.WebRootPath, imagePath.TrimStart('/'));
        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);
    }

    #region Excel 匯出 / 匯入 / 下載範本

    public async Task<IActionResult> Export([FromQuery] int[]? ids, int? brandId)
    {
        var query = _context.PrinterModels
            .Include(m => m.Brand)
            .AsQueryable();

        if (ids != null && ids.Length > 0)
        {
            query = query.Where(m => ids.Contains(m.Id));
        }
        else if (brandId.HasValue)
        {
            query = query.Where(m => m.BrandId == brandId.Value);
        }

        var items = await query
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Brand!.Name)
            .ThenBy(m => m.Name)
            .ToListAsync();

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("型號資料");

        var headers = new[] { "代碼", "名稱", "廠牌名稱", "特性", "說明" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        int row = 2;
        foreach (var item in items)
        {
            ws.Cell(row, 1).Value = item.Code;
            ws.Cell(row, 2).Value = item.Name;
            ws.Cell(row, 3).Value = item.Brand?.Name;
            ws.Cell(row, 4).Value = item.Feature;
            ws.Cell(row, 5).Value = item.Description;
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
            $"型號資料_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    public IActionResult DownloadSample()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("型號資料");

        var headers = new[] { "代碼", "名稱", "廠牌名稱", "特性", "說明" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        ws.Cell(2, 1).Value = "IR-C3025";
        ws.Cell(2, 2).Value = "imageRUNNER C3025";
        ws.Cell(2, 3).Value = "Canon";
        ws.Cell(2, 4).Value = "彩色多功能";
        ws.Cell(2, 5).Value = "A3 彩色影印";

        ws.Cell(3, 1).Value = "MP-C2003";
        ws.Cell(3, 2).Value = "Aficio MP C2003";
        ws.Cell(3, 3).Value = "Ricoh";
        ws.Cell(3, 4).Value = "彩色多功能";
        ws.Cell(3, 5).Value = "";

        var headerRange = ws.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "型號匯入範本.xlsx");
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
                    var code = GetCellValue(worksheet, row, headers, "代碼");
                    var name = GetCellValue(worksheet, row, headers, "名稱");
                    var brandName = GetCellValue(worksheet, row, headers, "廠牌名稱")
                                    ?? GetCellValue(worksheet, row, headers, "廠牌代碼");

                    if (string.IsNullOrWhiteSpace(code))
                    {
                        errors.Add($"第 {row} 列: 代碼為空，已跳過");
                        failed++;
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        errors.Add($"第 {row} 列: 名稱為空，已跳過");
                        failed++;
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(brandName))
                    {
                        errors.Add($"第 {row} 列: 廠牌名稱為空，已跳過");
                        failed++;
                        continue;
                    }

                    var brand = await _context.Brands.FirstOrDefaultAsync(b => b.Name == brandName);
                    if (brand == null)
                    {
                        errors.Add($"第 {row} 列: 找不到廠牌「{brandName}」，已跳過");
                        failed++;
                        continue;
                    }

                    var feature = GetCellValue(worksheet, row, headers, "特性");
                    var description = GetCellValue(worksheet, row, headers, "說明");

                    var existing = await _context.PrinterModels.FirstOrDefaultAsync(m => m.Code == code);
                    if (existing != null)
                    {
                        existing.Name = name;
                        existing.BrandId = brand.Id;
                        existing.Feature = feature;
                        existing.Description = description;
                    }
                    else
                    {
                        _context.PrinterModels.Add(new PrinterModel
                        {
                            Code = code,
                            Name = name,
                            BrandId = brand.Id,
                            Feature = feature,
                            Description = description,
                            State = "1",
                            CreatedAt = DateTime.UtcNow
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

public class ReorderSheetTypesRequest
{
    public int ModelId { get; set; }
    public List<int> OrderedIds { get; set; } = new();
}

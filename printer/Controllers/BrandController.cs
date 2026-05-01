using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;

namespace printer.Controllers;

public class BrandController : Controller
{
    private readonly PrinterDbContext _context;
    private readonly IWebHostEnvironment _env;

    public BrandController(PrinterDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    public async Task<IActionResult> Index()
    {
        var brands = await _context.Brands
            .Include(b => b.Models)
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.Name)
            .ToListAsync();
        return View(brands);
    }

    [HttpPost]
    public async Task<IActionResult> Reorder([FromBody] List<int> orderedIds)
    {
        var brands = await _context.Brands.ToListAsync();
        for (int i = 0; i < orderedIds.Count; i++)
        {
            var b = brands.FirstOrDefault(x => x.Id == orderedIds[i]);
            if (b != null) b.SortOrder = i * 10;
        }
        await _context.SaveChangesAsync();
        return Ok();
    }

    public IActionResult Create()
    {
        return View(new Brand());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Brand brand, IFormFile? image)
    {
        if (ModelState.IsValid)
        {
            brand.ImagePath = await SaveImage(image, "brands");
            brand.CreatedAt = DateTime.UtcNow;
            _context.Brands.Add(brand);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(brand);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var brand = await _context.Brands.FindAsync(id);
        if (brand == null) return NotFound();
        return View(brand);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Brand brand, IFormFile? image)
    {
        if (id != brand.Id) return NotFound();

        if (ModelState.IsValid)
        {
            var existing = await _context.Brands.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Name = brand.Name;
            existing.Description = brand.Description;

            if (image != null)
            {
                DeleteImage(existing.ImagePath);
                existing.ImagePath = await SaveImage(image, "brands");
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(brand);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var brand = await _context.Brands
            .Include(b => b.Models)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (brand == null) return NotFound();

        if (brand.Models.Any())
        {
            TempData["Error"] = "此廠牌下有型號，無法刪除";
            return RedirectToAction(nameof(Index));
        }

        DeleteImage(brand.ImagePath);
        _context.Brands.Remove(brand);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
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

    public async Task<IActionResult> Export([FromQuery] int[]? ids)
    {
        var query = _context.Brands.AsQueryable();
        if (ids != null && ids.Length > 0)
            query = query.Where(b => ids.Contains(b.Id));

        var items = await query.OrderBy(b => b.SortOrder).ThenBy(b => b.Name).ToListAsync();

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("廠牌資料");

        var headers = new[] { "名稱", "說明" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        int row = 2;
        foreach (var item in items)
        {
            ws.Cell(row, 1).Value = item.Name;
            ws.Cell(row, 2).Value = item.Description;
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
            $"廠牌資料_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    public IActionResult DownloadSample()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("廠牌資料");

        var headers = new[] { "名稱", "說明" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        ws.Cell(2, 1).Value = "Canon";
        ws.Cell(2, 2).Value = "Canon 廠牌說明";
        ws.Cell(3, 1).Value = "Ricoh";
        ws.Cell(3, 2).Value = "Ricoh 廠牌說明";

        var headerRange = ws.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "廠牌匯入範本.xlsx");
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

                    var description = GetCellValue(worksheet, row, headers, "說明");
                    var existing = await _context.Brands.FirstOrDefaultAsync(b => b.Name == name);
                    if (existing != null)
                    {
                        existing.Description = description;
                    }
                    else
                    {
                        _context.Brands.Add(new Brand
                        {
                            Name = name,
                            Description = description,
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

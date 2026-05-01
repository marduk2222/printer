using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;
using System.Security.Cryptography;
using System.Text;

namespace printer.Controllers;

[Authorize(Roles = "admin")]
public class AppUserController : Controller
{
    private readonly PrinterDbContext _context;

    public AppUserController(PrinterDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? role)
    {
        var query = _context.AppUsers.AsQueryable();

        if (!string.IsNullOrEmpty(role))
        {
            query = query.Where(u => u.Role == role);
        }

        var users = await query.OrderBy(u => u.SortOrder).ThenBy(u => u.DisplayName).ToListAsync();

        ViewBag.Role = role;
        ViewBag.Roles = GetRoleList();

        return View(users);
    }

    public IActionResult Create()
    {
        ViewBag.Roles = GetRoleList();
        return View(new AppUser());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppUser user, string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError("", "請輸入密碼");
        }

        if (await _context.AppUsers.AnyAsync(u => u.Username == user.Username))
        {
            ModelState.AddModelError("Username", "帳號已存在");
        }

        if (ModelState.IsValid)
        {
            user.PasswordHash = HashPassword(password);
            user.CreatedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"使用者 {user.DisplayName} 已建立";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Roles = GetRoleList();
        return View(user);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var user = await _context.AppUsers.FindAsync(id);
        if (user == null) return NotFound();

        ViewBag.Roles = GetRoleList();
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AppUser user, string? newPassword)
    {
        if (id != user.Id) return NotFound();

        var existing = await _context.AppUsers.FindAsync(id);
        if (existing == null) return NotFound();

        if (await _context.AppUsers.AnyAsync(u => u.Username == user.Username && u.Id != id))
        {
            ModelState.AddModelError("Username", "帳號已存在");
        }

        if (ModelState.IsValid)
        {
            existing.Username = user.Username;
            existing.DisplayName = user.DisplayName;
            existing.Email = user.Email;
            existing.Phone = user.Phone;
            existing.Role = user.Role;
            existing.IsActive = user.IsActive;
            existing.Note = user.Note;
            existing.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                existing.PasswordHash = HashPassword(newPassword);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"使用者 {existing.DisplayName} 已更新";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Roles = GetRoleList();
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _context.AppUsers.FindAsync(id);
        if (user != null)
        {
            _context.AppUsers.Remove(user);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"使用者 {user.DisplayName} 已刪除";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Reorder([FromBody] List<int> orderedIds)
    {
        for (int i = 0; i < orderedIds.Count; i++)
        {
            var user = await _context.AppUsers.FindAsync(orderedIds[i]);
            if (user != null) user.SortOrder = i;
        }
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(int id)
    {
        var user = await _context.AppUsers.FindAsync(id);
        if (user != null)
        {
            user.IsActive = !user.IsActive;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private static SelectList GetRoleList()
    {
        var roles = new[]
        {
            new { Value = "employee", Text = "員工" },
            new { Value = "supervisor", Text = "主管" },
            new { Value = "admin", Text = "系統管理員" }
        };
        return new SelectList(roles, "Value", "Text");
    }

    public static Dictionary<string, string> RoleNames => new()
    {
        ["employee"] = "員工",
        ["supervisor"] = "主管",
        ["admin"] = "系統管理員"
    };

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    #region Excel 匯出 / 匯入 / 下載範本

    public async Task<IActionResult> Export([FromQuery] int[]? ids, string? role)
    {
        var query = _context.AppUsers.AsQueryable();

        if (ids != null && ids.Length > 0)
        {
            query = query.Where(u => ids.Contains(u.Id));
        }
        else if (!string.IsNullOrEmpty(role))
        {
            query = query.Where(u => u.Role == role);
        }

        var items = await query
            .OrderBy(u => u.SortOrder)
            .ThenBy(u => u.DisplayName)
            .ToListAsync();

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("人員資料");

        var headers = new[] { "帳號", "顯示名稱", "Email", "角色", "啟用" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        int row = 2;
        foreach (var item in items)
        {
            ws.Cell(row, 1).Value = item.Username;
            ws.Cell(row, 2).Value = item.DisplayName;
            ws.Cell(row, 3).Value = item.Email;
            ws.Cell(row, 4).Value = item.Role;
            ws.Cell(row, 5).Value = item.IsActive ? "是" : "否";
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
            $"人員資料_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    public IActionResult DownloadSample()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("人員資料");

        var headers = new[] { "帳號", "顯示名稱", "Email", "角色", "啟用" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        ws.Cell(2, 1).Value = "user01";
        ws.Cell(2, 2).Value = "員工一";
        ws.Cell(2, 3).Value = "user01@example.com";
        ws.Cell(2, 4).Value = "employee";
        ws.Cell(2, 5).Value = "是";

        ws.Cell(3, 1).Value = "sup01";
        ws.Cell(3, 2).Value = "主管甲";
        ws.Cell(3, 3).Value = "sup01@example.com";
        ws.Cell(3, 4).Value = "supervisor";
        ws.Cell(3, 5).Value = "是";

        var headerRange = ws.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "人員匯入範本.xlsx");
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
            var validRoles = new[] { "admin", "supervisor", "employee" };
            var defaultPasswordHash = HashPassword("ChangeMe123!");

            for (int row = 2; row <= worksheet.RowsUsed().Count(); row++)
            {
                try
                {
                    var username = GetCellValue(worksheet, row, headers, "帳號");
                    if (string.IsNullOrWhiteSpace(username))
                    {
                        errors.Add($"第 {row} 列: 帳號為空，已跳過");
                        failed++;
                        continue;
                    }

                    var displayName = GetCellValue(worksheet, row, headers, "顯示名稱") ?? username;
                    var email = GetCellValue(worksheet, row, headers, "Email");
                    var roleVal = (GetCellValue(worksheet, row, headers, "角色") ?? "employee").ToLower();
                    if (!validRoles.Contains(roleVal))
                    {
                        errors.Add($"第 {row} 列: 角色「{roleVal}」無效，已跳過");
                        failed++;
                        continue;
                    }

                    var isActiveStr = GetCellValue(worksheet, row, headers, "啟用") ?? "是";
                    var isActive = isActiveStr == "是" || string.Equals(isActiveStr, "true", StringComparison.OrdinalIgnoreCase) || isActiveStr == "1" || string.Equals(isActiveStr, "yes", StringComparison.OrdinalIgnoreCase);

                    var existing = await _context.AppUsers.FirstOrDefaultAsync(u => u.Username == username);
                    if (existing != null)
                    {
                        existing.DisplayName = displayName;
                        existing.Email = email;
                        existing.Role = roleVal;
                        existing.IsActive = isActive;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        _context.AppUsers.Add(new AppUser
                        {
                            Username = username,
                            DisplayName = displayName,
                            Email = email,
                            Role = roleVal,
                            IsActive = isActive,
                            PasswordHash = defaultPasswordHash,
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
            msg += "。新使用者預設密碼為 ChangeMe123!";
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

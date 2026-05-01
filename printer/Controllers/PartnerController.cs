using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;

namespace printer.Controllers;

/// <summary>
/// 客戶管理頁面
/// </summary>
public class PartnerController : Controller
{
    private readonly PrinterDbContext _context;

    public PartnerController(PrinterDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? keyword, bool? isActive, int page = 1)
    {
        var query = _context.Partners.AsQueryable();

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(p =>
                p.Name.Contains(keyword) ||
                p.Code.Contains(keyword) ||
                (p.PartnerNumber != null && p.PartnerNumber.Contains(keyword)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        var pageSize = 20;
        var total = await query.CountAsync();
        var items = await query
            .Include(p => p.Printers)
            .OrderByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Keyword = keyword;
        ViewBag.IsActive = isActive;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        ViewBag.Total = total;
        ViewBag.ActiveCount = await _context.Partners.CountAsync(p => p.IsActive);

        // 合約到期提醒 (該客戶下的事務機)
        var contractPrinters = await _context.Printers
            .Where(p => p.IsActive && p.ContractEndDate.HasValue)
            .ToListAsync();
        ViewBag.ContractAlertCount = contractPrinters.Count(p => p.IsContractExpiringSoon || p.IsContractExpired);

        return View(items);
    }

    public async Task<IActionResult> Details(int id)
    {
        var partner = await _context.Partners
            .Include(p => p.Printers)
            .Include(p => p.Contacts)
            .Include(p => p.BillingInfos)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (partner == null)
            return NotFound();

        partner.Contacts = partner.Contacts.OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToList();
        partner.BillingInfos = partner.BillingInfos.OrderBy(b => b.SortOrder).ThenBy(b => b.Id).ToList();
        partner.Printers = partner.Printers.OrderBy(pr => pr.SortOrder).ThenBy(pr => pr.Id).ToList();

        return View(partner);
    }

    public async Task<IActionResult> Create()
    {
        var now = DateTime.Now;
        var prefix = now.ToString("yyyyMM");
        var maxCode = await _context.Partners
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

        var partner = new Partner { Code = $"{prefix}{seq:D3}" };
        return View(partner);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Partner partner)
    {
        if (ModelState.IsValid)
        {
            // 發票統編 / 公司名稱留空時，自動帶入基本資料對應欄位，讓帳單/發票直接使用 Invoice* 欄位
            if (string.IsNullOrWhiteSpace(partner.InvoiceTaxId))
                partner.InvoiceTaxId = partner.Vat;
            if (string.IsNullOrWhiteSpace(partner.InvoiceTitle))
                partner.InvoiceTitle = partner.Name;

            partner.CreatedAt = DateTime.UtcNow;
            partner.UpdatedAt = DateTime.UtcNow;
            _context.Partners.Add(partner);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(partner);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var partner = await _context.Partners.FindAsync(id);
        if (partner == null)
            return NotFound();

        return View(partner);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Partner partner, string? returnTo)
    {
        if (id != partner.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            var existing = await _context.Partners.FindAsync(id);
            if (existing == null)
                return NotFound();

            existing.Code = partner.Code;
            existing.PartnerNumber = partner.PartnerNumber;
            existing.Name = partner.Name;
            existing.Vat = partner.Vat;
            existing.Phone = partner.Phone;
            existing.Fax = partner.Fax;
            existing.Mobile = partner.Mobile;
            existing.Email = partner.Email;
            existing.Address = partner.Address;
            existing.ContactName = partner.ContactName;
            existing.Note = partner.Note;
            // 發票統編 / 公司名稱留空時自動帶入基本資料對應欄位（partner.Vat / partner.Name 為本次表單新值）
            existing.InvoiceTaxId = string.IsNullOrWhiteSpace(partner.InvoiceTaxId)
                ? partner.Vat
                : partner.InvoiceTaxId;
            existing.InvoiceTitle = string.IsNullOrWhiteSpace(partner.InvoiceTitle)
                ? partner.Name
                : partner.InvoiceTitle;
            existing.InvoiceCarrierType = partner.InvoiceCarrierType;
            existing.InvoiceCarrierId = partner.InvoiceCarrierId;
            existing.InvoiceDonationCode = partner.InvoiceDonationCode;
            existing.InvoiceEmail = partner.InvoiceEmail;
            existing.InvoiceDefaultTaxType = partner.InvoiceDefaultTaxType;
            existing.InvoiceNote = partner.InvoiceNote;
            existing.IsActive = partner.IsActive;
            existing.State = partner.State;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            if (returnTo == "Details")
                return RedirectToAction(nameof(Details), new { id });

            return RedirectToAction(nameof(Index));
        }
        return View(partner);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var partner = await _context.Partners.FindAsync(id);
        if (partner != null)
        {
            _context.Partners.Remove(partner);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var partner = await _context.Partners.FindAsync(id);
        if (partner == null)
            return NotFound();

        partner.IsActive = !partner.IsActive;
        partner.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Json(new { success = true, isActive = partner.IsActive });
    }

    /// <summary>
    /// 提供 Einvoice 建立頁面切換客戶時自動帶入發票資訊
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetInvoiceInfo(int id)
    {
        var p = await _context.Partners.FindAsync(id);
        if (p == null) return NotFound();

        return Json(new
        {
            buyerTaxId = string.IsNullOrWhiteSpace(p.InvoiceTaxId) ? p.Vat : p.InvoiceTaxId,
            buyerName = string.IsNullOrWhiteSpace(p.InvoiceTitle) ? p.Name : p.InvoiceTitle,
            buyerEmail = string.IsNullOrWhiteSpace(p.InvoiceEmail) ? p.Email : p.InvoiceEmail,
            carrierType = p.InvoiceCarrierType,
            carrierId = p.InvoiceCarrierId,
            donationCode = p.InvoiceDonationCode,
            taxType = p.InvoiceDefaultTaxType,
            note = p.InvoiceNote
        });
    }

    #region 連絡人 AJAX API

    [HttpPost]
    public async Task<IActionResult> AddContact(int partnerId, [FromForm] PartnerContact contact)
    {
        var partner = await _context.Partners.FindAsync(partnerId);
        if (partner == null) return NotFound();

        contact.PartnerId = partnerId;
        contact.CreatedAt = DateTime.UtcNow;
        contact.UpdatedAt = DateTime.UtcNow;
        _context.PartnerContacts.Add(contact);
        await _context.SaveChangesAsync();

        return Json(new { success = true, id = contact.Id });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateContact(int id, [FromForm] PartnerContact contact)
    {
        var existing = await _context.PartnerContacts.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Name = contact.Name;
        existing.Phone = contact.Phone;
        existing.Mobile = contact.Mobile;
        existing.Address = contact.Address;
        existing.Email = contact.Email;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteContact(int id)
    {
        var contact = await _context.PartnerContacts.FindAsync(id);
        if (contact == null) return NotFound();

        _context.PartnerContacts.Remove(contact);
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    #endregion

    #region 帳單資訊 AJAX API

    [HttpPost]
    public async Task<IActionResult> AddBillingInfo(int partnerId, [FromForm] PartnerBillingInfo info)
    {
        var partner = await _context.Partners.FindAsync(partnerId);
        if (partner == null) return NotFound();

        info.PartnerId = partnerId;
        info.CreatedAt = DateTime.UtcNow;
        info.UpdatedAt = DateTime.UtcNow;
        _context.PartnerBillingInfos.Add(info);
        await _context.SaveChangesAsync();

        return Json(new { success = true, id = info.Id });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateBillingInfo(int id, [FromForm] PartnerBillingInfo info)
    {
        var existing = await _context.PartnerBillingInfos.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Email = info.Email;
        existing.Address = info.Address;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteBillingInfo(int id)
    {
        var info = await _context.PartnerBillingInfos.FindAsync(id);
        if (info == null) return NotFound();

        _context.PartnerBillingInfos.Remove(info);
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    #endregion

    #region 手動排序

    /// <summary>
    /// 客戶詳情各列表的拖曳排序：依傳入順序寫回 SortOrder（從 1 起算）。
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReorderContacts(int partnerId, [FromForm] int[] ids)
    {
        var items = await _context.PartnerContacts
            .Where(c => c.PartnerId == partnerId && ids.Contains(c.Id))
            .ToListAsync();
        var indexMap = ids.Select((id, i) => new { id, i }).ToDictionary(x => x.id, x => x.i + 1);
        foreach (var c in items)
        {
            if (indexMap.TryGetValue(c.Id, out var order))
            {
                c.SortOrder = order;
                c.UpdatedAt = DateTime.UtcNow;
            }
        }
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> ReorderBillingInfos(int partnerId, [FromForm] int[] ids)
    {
        var items = await _context.PartnerBillingInfos
            .Where(b => b.PartnerId == partnerId && ids.Contains(b.Id))
            .ToListAsync();
        var indexMap = ids.Select((id, i) => new { id, i }).ToDictionary(x => x.id, x => x.i + 1);
        foreach (var b in items)
        {
            if (indexMap.TryGetValue(b.Id, out var order))
            {
                b.SortOrder = order;
                b.UpdatedAt = DateTime.UtcNow;
            }
        }
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> ReorderPrinters(int partnerId, [FromForm] int[] ids)
    {
        var items = await _context.Printers
            .Where(p => p.PartnerId == partnerId && ids.Contains(p.Id))
            .ToListAsync();
        var indexMap = ids.Select((id, i) => new { id, i }).ToDictionary(x => x.id, x => x.i + 1);
        foreach (var p in items)
        {
            if (indexMap.TryGetValue(p.Id, out var order))
            {
                p.SortOrder = order;
                p.UpdatedAt = DateTime.UtcNow;
            }
        }
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    #endregion

    #region Excel 匯出

    public async Task<IActionResult> Export([FromQuery] int[]? ids, string? keyword, bool? isActive)
    {
        var query = _context.Partners.AsQueryable();

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
                    (p.PartnerNumber != null && p.PartnerNumber.Contains(keyword)));
            }

            if (isActive.HasValue)
            {
                query = query.Where(p => p.IsActive == isActive.Value);
            }
        }

        var items = await query
            .Include(p => p.Printers)
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("客戶資料");

        var headers = new[] {
            "代碼", "客戶編號", "公司名稱", "統一編號", "公司電話", "傳真", "手機", "Email", "地址", "備註",
            "發票抬頭", "發票統編", "發票寄送Email", "載具類型", "載具號碼", "愛心碼", "預設課稅別", "發票備註"
        };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        int row = 2;
        foreach (var item in items)
        {
            ws.Cell(row, 1).Value = item.Code;
            ws.Cell(row, 2).Value = item.PartnerNumber;
            ws.Cell(row, 3).Value = item.Name;
            ws.Cell(row, 4).Value = item.Vat;
            ws.Cell(row, 5).Value = item.Phone;
            ws.Cell(row, 6).Value = item.Fax;
            ws.Cell(row, 7).Value = item.Mobile;
            ws.Cell(row, 8).Value = item.Email;
            ws.Cell(row, 9).Value = item.Address;
            ws.Cell(row, 10).Value = item.Note;
            ws.Cell(row, 11).Value = item.InvoiceTitle;
            ws.Cell(row, 12).Value = item.InvoiceTaxId;
            ws.Cell(row, 13).Value = item.InvoiceEmail;
            ws.Cell(row, 14).Value = item.InvoiceCarrierType;
            ws.Cell(row, 15).Value = item.InvoiceCarrierId;
            ws.Cell(row, 16).Value = item.InvoiceDonationCode;
            ws.Cell(row, 17).Value = item.InvoiceDefaultTaxType;
            ws.Cell(row, 18).Value = item.InvoiceNote;
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
            $"客戶資料_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    #endregion

    #region Excel 匯入

    public IActionResult Import()
    {
        return View();
    }

    public IActionResult DownloadSample()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("客戶資料");

        var headers = new[] {
            "客戶編號", "公司名稱", "統一編號", "公司電話", "傳真", "手機", "Email", "地址", "備註",
            "發票抬頭", "發票統編", "發票寄送Email", "載具類型", "載具號碼", "愛心碼", "預設課稅別", "發票備註"
        };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        // 範例資料
        ws.Cell(2, 1).Value = "C001";
        ws.Cell(2, 2).Value = "範例科技有限公司";
        ws.Cell(2, 3).Value = "12345678";
        ws.Cell(2, 4).Value = "02-12345678";
        ws.Cell(2, 5).Value = "02-12345679";
        ws.Cell(2, 6).Value = "0912345678";
        ws.Cell(2, 7).Value = "example@company.com";
        ws.Cell(2, 8).Value = "台北市中正區範例路1號";
        ws.Cell(2, 9).Value = "這是備註";
        ws.Cell(2, 10).Value = "(留空則用公司名稱)";
        ws.Cell(2, 11).Value = "(留空則用統一編號)";
        ws.Cell(2, 12).Value = "invoice@company.com";
        ws.Cell(2, 13).Value = "(none/mobile/citizen/donate)";
        ws.Cell(2, 14).Value = "/AB+12.3";
        ws.Cell(2, 15).Value = "";
        ws.Cell(2, 16).Value = "(taxable/zero/free)";
        ws.Cell(2, 17).Value = "";

        // 設定標題列樣式
        var headerRange = ws.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "客戶匯入範本.xlsx");
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
            var maxCode = await _context.Partners
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
                    var name = GetCellValue(worksheet, row, headers, "公司名稱");
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        errors.Add($"第 {row} 列: 公司名稱為空，已跳過");
                        failed++;
                        continue;
                    }

                    var partner = new Partner
                    {
                        Code = $"{prefix}{seq:D3}",
                        PartnerNumber = GetCellValue(worksheet, row, headers, "客戶編號"),
                        Name = name,
                        Vat = GetCellValue(worksheet, row, headers, "統一編號"),
                        Phone = GetCellValue(worksheet, row, headers, "公司電話"),
                        Fax = GetCellValue(worksheet, row, headers, "傳真"),
                        Mobile = GetCellValue(worksheet, row, headers, "手機"),
                        Email = GetCellValue(worksheet, row, headers, "Email"),
                        Address = GetCellValue(worksheet, row, headers, "地址"),
                        Note = GetCellValue(worksheet, row, headers, "備註"),
                        InvoiceTitle = GetCellValue(worksheet, row, headers, "發票抬頭"),
                        InvoiceTaxId = GetCellValue(worksheet, row, headers, "發票統編"),
                        InvoiceEmail = GetCellValue(worksheet, row, headers, "發票寄送Email"),
                        InvoiceCarrierType = GetCellValue(worksheet, row, headers, "載具類型"),
                        InvoiceCarrierId = GetCellValue(worksheet, row, headers, "載具號碼"),
                        InvoiceDonationCode = GetCellValue(worksheet, row, headers, "愛心碼"),
                        InvoiceDefaultTaxType = GetCellValue(worksheet, row, headers, "預設課稅別"),
                        InvoiceNote = GetCellValue(worksheet, row, headers, "發票備註"),
                        IsCompany = true,
                        IsActive = true,
                        State = "1",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    // 發票抬頭/統編留空時自動帶入
                    if (string.IsNullOrWhiteSpace(partner.InvoiceTitle)) partner.InvoiceTitle = partner.Name;
                    if (string.IsNullOrWhiteSpace(partner.InvoiceTaxId)) partner.InvoiceTaxId = partner.Vat;

                    _context.Partners.Add(partner);
                    seq++;
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

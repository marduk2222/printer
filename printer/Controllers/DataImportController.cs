using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;
using printer.Data.Enums;

namespace printer.Controllers;

/// <summary>
/// 合併匯入：單一 Excel 工作表，每列代表一台事務機，並把所屬客戶欄位寫在同一列。
/// 匯入時依「客戶編號 → 公司名稱」判斷客戶是否已存在：
///   已存在（含本次匯入中已建立者）→ 沿用；不存在 → 自動建立客戶。
/// 同一客戶出現在多列只會建立一次，再把多台事務機掛在其下。
/// 計費欄位（月租費用、各張數類型「{類型}單價／{類型}贈送／{類型}誤印率／{類型}互換比例」）
/// 任一有填時，一併建立計費設定與張數類型單價。
/// </summary>
public class DataImportController : Controller
{
    private readonly PrinterDbContext _context;

    public DataImportController(PrinterDbContext context)
    {
        _context = context;
    }

    public IActionResult Index() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "請選擇檔案";
            return View("Index");
        }

        try
        {
            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var ws = workbook.Worksheets.First();
            var headers = ReadHeaders(ws);

            var prefix = DateTime.Now.ToString("yyyyMM");
            var partnerSeq = await NextSequenceAsync(_context.Partners.Select(p => p.Code), prefix);
            var printerSeq = await NextSequenceAsync(_context.Printers.Select(p => p.Code), prefix);

            // 本次匯入中已解析過的客戶（避免同一客戶建立多次）
            var partnerCache = new Dictionary<string, Partner>(StringComparer.OrdinalIgnoreCase);

            // 啟用中的張數類型（供「{類型}單價」等動態計費欄位解析）
            var sheetTypes = await _context.SheetTypes
                .Where(st => st.IsActive)
                .OrderBy(st => st.SortOrder).ThenBy(st => st.Id)
                .ToListAsync();

            int partnersCreated = 0, partnersReused = 0, printersCreated = 0, billingCreated = 0, failed = 0;
            var errors = new List<string>();

            for (int row = 2; row <= ws.RowsUsed().Count(); row++)
            {
                try
                {
                    var printerName = GetCellValue(ws, row, headers, "名稱");
                    if (string.IsNullOrWhiteSpace(printerName))
                    {
                        errors.Add($"第 {row} 列: 事務機名稱為空，已跳過");
                        failed++;
                        continue;
                    }

                    // 1) 解析 / 建立客戶
                    var partnerKey = GetCellValue(ws, row, headers, "客戶編號");
                    var companyName = GetCellValue(ws, row, headers, "公司名稱");
                    var (partner, created) = await ResolvePartnerAsync(
                        ws, row, headers, partnerKey, companyName, prefix, partnerCache,
                        () => $"{prefix}{partnerSeq++:D3}");
                    if (created) partnersCreated++;
                    else if (partner != null) partnersReused++;

                    // 2) 廠牌 / 型號
                    var (brandId, modelId) = await ResolveBrandModelAsync(
                        GetCellValue(ws, row, headers, "廠牌"),
                        GetCellValue(ws, row, headers, "型號"));

                    // 3) 建立事務機
                    var code = GetCellValue(ws, row, headers, "代碼");
                    if (string.IsNullOrWhiteSpace(code))
                        code = $"{prefix}{printerSeq++:D3}";

                    DateOnly? contractStart = null, contractEnd = null;
                    var startStr = GetCellValue(ws, row, headers, "合約開始日期");
                    var endStr = GetCellValue(ws, row, headers, "合約結束日期");
                    if (!string.IsNullOrWhiteSpace(startStr) && DateTime.TryParse(startStr, out var csDate))
                        contractStart = DateOnly.FromDateTime(csDate);
                    if (!string.IsNullOrWhiteSpace(endStr) && DateTime.TryParse(endStr, out var ceDate))
                        contractEnd = DateOnly.FromDateTime(ceDate);

                    decimal? deposit = null;
                    var depositStr = GetCellValue(ws, row, headers, "押金");
                    if (!string.IsNullOrWhiteSpace(depositStr) && decimal.TryParse(depositStr, out var depVal))
                        deposit = depVal;

                    var printerEntity = new Printer
                    {
                        Code = code,
                        Name = printerName,
                        Partner = partner,                       // 導航屬性；EF 會在存檔時解析 PartnerId（含本次新建客戶）
                        PartnerNumber = partner?.PartnerNumber,
                        BrandId = brandId,
                        ModelId = modelId,
                        SerialNumber = GetCellValue(ws, row, headers, "序號"),
                        Ip = GetCellValue(ws, row, headers, "IP"),
                        Mac = GetCellValue(ws, row, headers, "MAC"),
                        PrinterName = GetCellValue(ws, row, headers, "印表機名稱"),
                        ContractStartDate = contractStart,
                        ContractEndDate = contractEnd,
                        Deposit = deposit,
                        Description = GetCellValue(ws, row, headers, "說明"),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Printers.Add(printerEntity);
                    printersCreated++;

                    // 4) 計費設定（月租 + 各張數類型單價；任一計費欄位有填才建立）
                    if (ImportBillingConfig(ws, row, headers, printerEntity, sheetTypes, contractStart))
                        billingCreated++;
                }
                catch (Exception ex)
                {
                    errors.Add($"第 {row} 列: {ex.Message}");
                    failed++;
                }
            }

            await _context.SaveChangesAsync();

            var msg = $"匯入完成：新建客戶 {partnersCreated} 筆";
            if (partnersReused > 0) msg += $"（沿用既有客戶 {partnersReused} 筆）";
            msg += $"，事務機 {printersCreated} 筆";
            if (billingCreated > 0) msg += $"，計費設定 {billingCreated} 筆";
            if (failed > 0) msg += $"，失敗 {failed} 筆";
            TempData["Success"] = msg;

            if (errors.Any())
                TempData["Error"] = string.Join("\n", errors);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"檔案讀取失敗: {ex.Message}";
        }

        return View("Index");
    }

    /// <summary>
    /// 依「客戶編號 → 公司名稱」解析客戶；先查本次快取、再查資料庫，皆無則建立新客戶。
    /// 回傳 (客戶, 是否為本次新建)。兩者皆空白時回傳 (null, false)，事務機不掛客戶。
    /// </summary>
    private async Task<(Partner? partner, bool created)> ResolvePartnerAsync(
        IXLWorksheet ws, int row, Dictionary<string, int> headers,
        string? partnerKey, string? companyName, string prefix,
        Dictionary<string, Partner> cache, Func<string> nextCode)
    {
        var cacheKey = !string.IsNullOrWhiteSpace(partnerKey) ? "N:" + partnerKey
                     : !string.IsNullOrWhiteSpace(companyName) ? "M:" + companyName
                     : null;
        if (cacheKey == null) return (null, false);

        if (cache.TryGetValue(cacheKey, out var cached))
            return (cached, false);

        // 資料庫既有客戶：客戶編號比對 PartnerNumber / Code，其次公司名稱
        Partner? existing = null;
        if (!string.IsNullOrWhiteSpace(partnerKey))
            existing = await _context.Partners
                .FirstOrDefaultAsync(p => p.PartnerNumber == partnerKey || p.Code == partnerKey);
        if (existing == null && !string.IsNullOrWhiteSpace(companyName))
            existing = await _context.Partners.FirstOrDefaultAsync(p => p.Name == companyName);

        if (existing != null)
        {
            cache[cacheKey] = existing;
            return (existing, false);
        }

        // 建立新客戶（客戶欄位取自本列）
        var name = companyName ?? partnerKey!;
        var partner = new Partner
        {
            Code = nextCode(),
            PartnerNumber = partnerKey,
            Name = name,
            Vat = GetCellValue(ws, row, headers, "統一編號"),
            Phone = GetCellValue(ws, row, headers, "公司電話"),
            Fax = GetCellValue(ws, row, headers, "傳真"),
            Mobile = GetCellValue(ws, row, headers, "手機"),
            Email = GetCellValue(ws, row, headers, "Email"),
            Address = GetCellValue(ws, row, headers, "地址"),
            Note = GetCellValue(ws, row, headers, "備註"),
            InvoiceTitle = GetCellValue(ws, row, headers, "發票抬頭"),
            InvoiceTaxId = GetCellValue(ws, row, headers, "發票統編"),
            InvoiceEmail = GetCellValue(ws, row, headers, "發票寄送Email"),
            InvoiceCarrierType = GetCellValue(ws, row, headers, "載具類型"),
            InvoiceCarrierId = GetCellValue(ws, row, headers, "載具號碼"),
            InvoiceDonationCode = GetCellValue(ws, row, headers, "愛心碼"),
            InvoiceDefaultTaxType = GetCellValue(ws, row, headers, "預設課稅別"),
            InvoiceNote = GetCellValue(ws, row, headers, "發票備註"),
            IsCompany = true,
            IsActive = true,
            State = "1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        if (string.IsNullOrWhiteSpace(partner.InvoiceTitle)) partner.InvoiceTitle = partner.Name;
        if (string.IsNullOrWhiteSpace(partner.InvoiceTaxId)) partner.InvoiceTaxId = partner.Vat;

        _context.Partners.Add(partner);
        cache[cacheKey] = partner;
        return (partner, true);
    }

    /// <summary>
    /// 解析本列的計費欄位並建立計費設定（含各張數類型單價）。
    /// 「月租費用」或任一「{類型}單價／{類型}贈送／{類型}誤印率／{類型}互換比例」有填才建立；
    /// 起算日期未填時以合約開始日期代入。回傳是否有建立。
    /// </summary>
    private bool ImportBillingConfig(IXLWorksheet ws, int row, Dictionary<string, int> headers,
        Printer printer, List<SheetType> sheetTypes, DateOnly? contractStart)
    {
        var monthlyFee = GetDecimal(ws, row, headers, "月租費用");

        var prices = new List<PrinterBillingSheetPrice>();
        var sort = 1;
        foreach (var st in sheetTypes)
        {
            var unitPrice = GetDecimal(ws, row, headers, $"{st.Name}單價");
            var freePages = GetInt(ws, row, headers, $"{st.Name}贈送");
            var discount = GetDecimal(ws, row, headers, $"{st.Name}誤印率");
            var weight = GetDecimal(ws, row, headers, $"{st.Name}互換比例");
            if (unitPrice == null && freePages == null && discount == null && weight == null)
                continue;

            prices.Add(new PrinterBillingSheetPrice
            {
                Printer = printer,                   // 導航屬性；EF 存檔時解析 PrinterId
                SheetTypeId = st.Id,
                UnitPrice = unitPrice ?? 0,
                DiscountPercent = discount ?? 0,
                FreePages = freePages ?? 0,
                Weight = weight,
                SortOrder = sort++,
                UpdatedAt = DateTime.UtcNow
            });
        }

        if (monthlyFee == null && prices.Count == 0) return false;

        _context.PrinterBillingConfigs.Add(new PrinterBillingConfig
        {
            Printer = printer,
            IsEnabled = true,
            // 有張數單價時設為 PerPage，計費設定頁的「張數計費」開關才會呈現啟用
            PageMethod = prices.Count > 0 ? PageMethod.PerPage : PageMethod.None,
            MonthlyFee = monthlyFee ?? 0,
            MonthlyFeeCycle = GetInt(ws, row, headers, "月租週期") ?? 1,
            PageFeeCycle = GetInt(ws, row, headers, "張數週期") ?? 1,
            MonthlyStartDate = GetDate(ws, row, headers, "月租起算日期") ?? contractStart,
            PageStartDate = GetDate(ws, row, headers, "張數起算日期") ?? contractStart,
            Note = GetCellValue(ws, row, headers, "計費備註"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _context.PrinterBillingSheetPrices.AddRange(prices);
        return true;
    }

    /// <summary>依名稱或代碼解析廠牌與型號。</summary>
    private async Task<(int? brandId, int? modelId)> ResolveBrandModelAsync(string? brandName, string? modelName)
    {
        int? brandId = null, modelId = null;
        if (!string.IsNullOrWhiteSpace(brandName))
        {
            var brand = await _context.Brands
                .FirstOrDefaultAsync(b => b.Name == brandName || b.Code == brandName);
            if (brand != null) brandId = brand.Id;
        }
        if (!string.IsNullOrWhiteSpace(modelName))
        {
            PrinterModel? model = null;
            if (brandId.HasValue)
                model = await _context.PrinterModels.FirstOrDefaultAsync(m =>
                    m.BrandId == brandId && (m.Name == modelName || m.Code == modelName));
            model ??= await _context.PrinterModels
                .FirstOrDefaultAsync(m => m.Name == modelName || m.Code == modelName);
            if (model != null)
            {
                modelId = model.Id;
                brandId ??= model.BrandId;
            }
        }
        return (brandId, modelId);
    }

    #region 合併範本

    public async Task<IActionResult> DownloadSample()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("客戶與事務機");

        // 前段為客戶欄位（同客戶可重複多列）、中段為事務機欄位、後段為計費欄位
        var headers = new List<string>
        {
            "客戶編號", "公司名稱", "統一編號", "公司電話", "傳真", "手機", "Email", "地址", "備註",
            "代碼", "名稱", "廠牌", "型號", "序號", "IP", "MAC", "印表機名稱",
            "合約開始日期", "合約結束日期", "押金", "說明",
            "月租費用", "月租週期", "張數週期", "月租起算日期", "張數起算日期", "計費備註"
        };

        // 各張數類型的計費欄位（依啟用中的張數類型動態產生）
        var sheetTypes = await _context.SheetTypes
            .Where(st => st.IsActive)
            .OrderBy(st => st.SortOrder).ThenBy(st => st.Id)
            .ToListAsync();
        foreach (var st in sheetTypes)
            headers.AddRange(new[] { $"{st.Name}單價", $"{st.Name}贈送", $"{st.Name}誤印率", $"{st.Name}互換比例" });

        var col = new Dictionary<string, int>();
        for (int i = 0; i < headers.Count; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            col[headers[i]] = i + 1;
        }

        // C001 客戶兩台機器（第一台含計費範例）
        WriteRow(ws, col, 2, "C001", "範例科技有限公司", "12345678", "02-12345678", "辦公室印表機", "Canon", "imageRUNNER C3025", "192.168.1.100");
        WriteBillingSample(ws, col, 2, sheetTypes);
        WriteRow(ws, col, 3, "C001", "範例科技有限公司", "12345678", "02-12345678", "一樓影印機", "Canon", "imageRUNNER C3025", "192.168.1.101");
        // C002 客戶一台機器
        WriteRow(ws, col, 4, "C002", "大大企業股份有限公司", "87654321", "04-87654321", "櫃台機", "Ricoh", "Aficio MP C2003", "192.168.2.50");

        var headerRange = ws.Range(1, 1, 1, headers.Count);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "客戶_事務機_合併匯入範本.xlsx");
    }

    private static void WriteRow(IXLWorksheet ws, Dictionary<string, int> col, int row,
        string partnerNo, string company, string vat, string phone,
        string printerName, string brand, string model, string ip)
    {
        ws.Cell(row, col["客戶編號"]).Value = partnerNo;
        ws.Cell(row, col["公司名稱"]).Value = company;
        ws.Cell(row, col["統一編號"]).Value = vat;
        ws.Cell(row, col["公司電話"]).Value = phone;
        ws.Cell(row, col["名稱"]).Value = printerName;
        ws.Cell(row, col["廠牌"]).Value = brand;
        ws.Cell(row, col["型號"]).Value = model;
        ws.Cell(row, col["IP"]).Value = ip;
    }

    /// <summary>填入計費範例值：月租 + 前兩種張數類型的單價／贈送。</summary>
    private static void WriteBillingSample(IXLWorksheet ws, Dictionary<string, int> col, int row, List<SheetType> sheetTypes)
    {
        ws.Cell(row, col["月租費用"]).Value = 1500;
        ws.Cell(row, col["合約開始日期"]).Value = "2026-01-01";
        ws.Cell(row, col["合約結束日期"]).Value = "2028-12-31";

        var samples = new[] { (Price: 0.4, Free: 1000), (Price: 2.5, Free: 0) };
        for (int i = 0; i < sheetTypes.Count && i < samples.Length; i++)
        {
            ws.Cell(row, col[$"{sheetTypes[i].Name}單價"]).Value = samples[i].Price;
            ws.Cell(row, col[$"{sheetTypes[i].Name}贈送"]).Value = samples[i].Free;
        }
    }

    #endregion

    #region Helpers

    private static Dictionary<string, int> ReadHeaders(IXLWorksheet ws)
    {
        var headers = new Dictionary<string, int>();
        var headerRow = ws.Row(1);
        for (int col = 1; col <= ws.ColumnsUsed().Count(); col++)
        {
            var header = headerRow.Cell(col).GetString().Trim();
            if (!string.IsNullOrEmpty(header))
                headers[header] = col;
        }
        return headers;
    }

    /// <summary>取得指定前綴（yyyyMM）的下一個流水號。</summary>
    private static async Task<int> NextSequenceAsync(IQueryable<string> codes, string prefix)
    {
        var maxCode = await codes
            .Where(c => c.StartsWith(prefix))
            .OrderByDescending(c => c)
            .FirstOrDefaultAsync();

        var seq = 1;
        if (maxCode != null && maxCode.Length > prefix.Length && int.TryParse(maxCode.Substring(prefix.Length), out var n))
            seq = n + 1;
        return seq;
    }

    private static string? GetCellValue(IXLWorksheet ws, int row, Dictionary<string, int> headers, string headerName)
    {
        if (!headers.TryGetValue(headerName, out var col)) return null;
        var value = ws.Cell(row, col).GetString().Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static decimal? GetDecimal(IXLWorksheet ws, int row, Dictionary<string, int> headers, string headerName)
    {
        var value = GetCellValue(ws, row, headers, headerName);
        return value != null && decimal.TryParse(value, out var d) ? d : null;
    }

    private static int? GetInt(IXLWorksheet ws, int row, Dictionary<string, int> headers, string headerName)
    {
        var value = GetCellValue(ws, row, headers, headerName);
        return value != null && int.TryParse(value, out var i) ? i : null;
    }

    private static DateOnly? GetDate(IXLWorksheet ws, int row, Dictionary<string, int> headers, string headerName)
    {
        var value = GetCellValue(ws, row, headers, headerName);
        return value != null && DateTime.TryParse(value, out var dt) ? DateOnly.FromDateTime(dt) : null;
    }

    #endregion
}

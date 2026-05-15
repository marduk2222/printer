using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;

namespace printer.Controllers.Api;

/// <summary>
/// 張數回傳紀錄 RESTful API
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class PrintRecordsController : ControllerBase
{
    private readonly PrinterDbContext _context;
    private readonly ILogger<PrintRecordsController> _logger;

    public PrintRecordsController(PrinterDbContext context, ILogger<PrintRecordsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 驅動上傳張數（支援動態參數）
    /// </summary>
    [HttpPost("upload")]
    public async Task<ActionResult> Upload([FromBody] PrintRecordUploadRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "請求內容不可為空" });

        // 記錄收到的原始上傳內容（含上傳 agent 識別），便於事後追蹤
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        _logger.LogInformation(
            "PrintRecord upload received from remote={RemoteIp} hostName={HostName} hostIp={HostIp} payload={Payload}",
            remoteIp,
            request.HostName,
            request.HostIp,
            JsonSerializer.Serialize(request));

        var printer = await _context.Printers
            .Include(p => p.Model)
            .FirstOrDefaultAsync(p => p.Id == request.PrinterId);

        if (printer == null)
            return NotFound(new { message = "找不到事務機" });

        // 驗證 MAC / 序號：避免 PrinterId 填錯把計數寫到錯的事務機
        // 規則（與 printer_info / printer_setup 對齊）：
        //   1. server 與 request 兩邊都有的識別欄位，任一不符 → 拒絕
        //   2. 請求的 mac 與 serialNumber 不可同時為空（雙空 = 無從驗證或無法首次寫入）
        bool printerHasMac    = !string.IsNullOrWhiteSpace(printer.Mac);
        bool printerHasSerial = !string.IsNullOrWhiteSpace(printer.SerialNumber);
        bool requestHasMac    = !string.IsNullOrWhiteSpace(request.Mac);
        bool requestHasSerial = !string.IsNullOrWhiteSpace(request.SerialNumber);

        bool macMismatch = printerHasMac && requestHasMac &&
            !string.Equals(printer.Mac!.Trim(), request.Mac!.Trim(), StringComparison.OrdinalIgnoreCase);
        bool serialMismatch = printerHasSerial && requestHasSerial &&
            !string.Equals(printer.SerialNumber!.Trim(), request.SerialNumber!.Trim(), StringComparison.OrdinalIgnoreCase);

        if (macMismatch || serialMismatch)
        {
            return BadRequest(new
            {
                message = "事務機 MAC 或序號不符",
                expected = new { mac = printer.Mac, serialNumber = printer.SerialNumber },
                received = new { mac = request.Mac, serialNumber = request.SerialNumber }
            });
        }

        if (!requestHasMac && !requestHasSerial)
        {
            return BadRequest(new
            {
                message = (printerHasMac || printerHasSerial)
                    ? "事務機已有 mac/序號紀錄，請於請求中提供 mac 或 serialNumber 以驗證身分"
                    : "首次抄表請於請求中提供 mac 或 serialNumber 以建立識別"
            });
        }

        bool printerIdentityChanged = false;
        if (!printerHasMac && requestHasMac)
        {
            printer.Mac = request.Mac!.Trim();
            printerIdentityChanged = true;
        }
        if (!printerHasSerial && requestHasSerial)
        {
            printer.SerialNumber = request.SerialNumber!.Trim();
            printerIdentityChanged = true;
        }
        if (printerIdentityChanged)
            printer.UpdatedAt = DateTime.UtcNow;

        // 取得此機型的張數類型與驅動參數對應
        Dictionary<string, int> keyToSheetType = new();
        if (printer.ModelId.HasValue)
        {
            var modelSheetTypeIds = await _context.PrinterModelSheetTypes
                .Where(m => m.PrinterModelId == printer.ModelId.Value)
                .Select(m => m.SheetTypeId)
                .ToListAsync();

            var keys = await _context.SheetTypeKeys
                .Where(k => modelSheetTypeIds.Contains(k.SheetTypeId))
                .ToListAsync();

            foreach (var k in keys)
                keyToSheetType[k.DriverKey] = k.SheetTypeId;
        }

        // 加總同 sheet_type 的多個 driver_key
        var sheetTotals = new Dictionary<int, int>();
        foreach (var kv in request.Values)
        {
            if (keyToSheetType.TryGetValue(kv.Key, out var sheetTypeId))
            {
                sheetTotals[sheetTypeId] = sheetTotals.GetValueOrDefault(sheetTypeId) + kv.Value;
            }
            // 找不到對應的 driver_key → 忽略
        }

        // 由 sheetTotals 反推舊欄位（黑白/彩色/大張），讓 Index/Excel/統計仍可用；
        // 對應不到名稱的類別只記在 PrintRecordValues
        var standardNames = new[] { "黑白", "彩色", "大張", "彩色大張" };
        var typeIds = sheetTotals.Keys.ToList();
        var nameById = await _context.SheetTypes
            .Where(st => typeIds.Contains(st.Id) && standardNames.Contains(st.Name))
            .ToDictionaryAsync(st => st.Id, st => st.Name);

        int legacyBlack = 0, legacyColor = 0, legacyLarge = 0;
        foreach (var (sheetTypeId, value) in sheetTotals)
        {
            if (!nameById.TryGetValue(sheetTypeId, out var name)) continue;
            switch (name)
            {
                case "黑白": legacyBlack += value; break;
                case "彩色": legacyColor += value; break;
                case "大張":
                case "彩色大張": legacyLarge += value; break;
            }
        }

        // 建立 PrintRecord
        var record = new PrintRecord
        {
            PrinterId = request.PrinterId,
            PartnerId = printer.PartnerId,
            Date = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow),
            BlackSheets = legacyBlack,
            ColorSheets = legacyColor,
            LargeSheets = legacyLarge,
            State = "auto",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.PrintRecords.Add(record);
        await _context.SaveChangesAsync();

        // 寫入 PrintRecordValues
        foreach (var (sheetTypeId, value) in sheetTotals)
        {
            _context.PrintRecordValues.Add(new PrintRecordValue
            {
                RecordId = record.Id,
                SheetTypeId = sheetTypeId,
                Value = value
            });
        }

        await _context.SaveChangesAsync();

        // sheetTotals 是 Dictionary<int,int>，System.Text.Json 預設不接受非 string key，先轉成 string-key dict 再序列化
        var sheetTotalsForLog = sheetTotals.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
        _logger.LogInformation(
            "PrintRecord upload succeeded: recordId={RecordId} printerId={PrinterId} hostName={HostName} hostIp={HostIp} sheetTotals={SheetTotals}",
            record.Id, request.PrinterId, request.HostName, request.HostIp, JsonSerializer.Serialize(sheetTotalsForLog));

        return Ok(new { recordId = record.Id, sheetTotals });
    }

    /// <summary>
    /// 取得所有抄表記錄
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PrintRecord>>> GetAll(
        [FromQuery] int? printerId,
        [FromQuery] int? partnerId,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        var query = _context.PrintRecords
            .Include(r => r.Printer)
            .Include(r => r.Partner)
            .AsQueryable();

        if (printerId.HasValue)
        {
            query = query.Where(r => r.PrinterId == printerId.Value);
        }

        if (partnerId.HasValue)
        {
            query = query.Where(r => r.PartnerId == partnerId.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(r => r.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(r => r.Date <= endDate.Value);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        Response.Headers.Append("X-Total-Count", total.ToString());
        return Ok(items);
    }

    /// <summary>
    /// 取得單一抄表記錄
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PrintRecord>> GetById(int id)
    {
        var record = await _context.PrintRecords
            .Include(r => r.Printer)
            .Include(r => r.Partner)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (record == null)
            return NotFound();

        return Ok(record);
    }

    /// <summary>
    /// 建立抄表記錄
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PrintRecord>> Create([FromBody] PrintRecord record)
    {
        record.CreatedAt = DateTime.UtcNow;
        record.UpdatedAt = DateTime.UtcNow;

        _context.PrintRecords.Add(record);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = record.Id }, record);
    }

    /// <summary>
    /// 更新抄表記錄
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] PrintRecord record)
    {
        if (id != record.Id)
            return BadRequest();

        var existing = await _context.PrintRecords.FindAsync(id);
        if (existing == null)
            return NotFound();

        existing.Date = record.Date;
        existing.BlackSheets = record.BlackSheets;
        existing.ColorSheets = record.ColorSheets;
        existing.LargeSheets = record.LargeSheets;
        existing.State = record.State;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// 刪除抄表記錄
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var record = await _context.PrintRecords.FindAsync(id);
        if (record == null)
            return NotFound();

        _context.PrintRecords.Remove(record);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// 取得統計資料
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult> GetStatistics(
        [FromQuery] int? printerId,
        [FromQuery] int? partnerId,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate)
    {
        var query = _context.PrintRecords.AsQueryable();

        if (printerId.HasValue)
            query = query.Where(r => r.PrinterId == printerId.Value);

        if (partnerId.HasValue)
            query = query.Where(r => r.PartnerId == partnerId.Value);

        if (startDate.HasValue)
            query = query.Where(r => r.Date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(r => r.Date <= endDate.Value);

        var stats = await query.GroupBy(r => 1).Select(g => new
        {
            TotalRecords = g.Count(),
            TotalBlackSheets = g.Sum(r => r.BlackSheets),
            TotalColorSheets = g.Sum(r => r.ColorSheets),
            TotalLargeSheets = g.Sum(r => r.LargeSheets),
            TotalSheets = g.Sum(r => r.BlackSheets + r.ColorSheets + r.LargeSheets)
        }).FirstOrDefaultAsync();

        return Ok(stats ?? new
        {
            TotalRecords = 0,
            TotalBlackSheets = 0,
            TotalColorSheets = 0,
            TotalLargeSheets = 0,
            TotalSheets = 0
        });
    }
}

/// <summary>
/// 驅動上傳張數請求
/// </summary>
public class PrintRecordUploadRequest
{
    /// <summary>
    /// 事務機 ID
    /// </summary>
    public int PrinterId { get; set; }

    /// <summary>
    /// 事務機 MAC（用於識別驗證；首次抄表至少須帶 mac 或 serialNumber 其一）
    /// </summary>
    public string? Mac { get; set; }

    /// <summary>
    /// 事務機序號（用於識別驗證；首次抄表至少須帶 mac 或 serialNumber 其一）
    /// </summary>
    public string? SerialNumber { get; set; }

    /// <summary>
    /// 上傳 agent 主機名稱（用於追蹤是哪台機器上傳）
    /// </summary>
    public string? HostName { get; set; }

    /// <summary>
    /// 上傳 agent 主機 IP（用於追蹤是哪台機器上傳）
    /// </summary>
    public string? HostIp { get; set; }

    /// <summary>
    /// 記錄日期（選填，預設今日）
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// 驅動回傳的參數與數值，例如：{ "print_black": 1000, "print_color": 50 }
    /// </summary>
    public Dictionary<string, int> Values { get; set; } = new();
}

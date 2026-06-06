using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;

namespace printer.Controllers.Api;

/// <summary>
/// 事務機 RESTful API
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class PrintersController : ControllerBase
{
    private readonly PrinterDbContext _context;
    private readonly Services.IPrinterLifecycleService _lifecycleService;

    public PrintersController(PrinterDbContext context, Services.IPrinterLifecycleService lifecycleService)
    {
        _context = context;
        _lifecycleService = lifecycleService;
    }

    /// <summary>
    /// 取得所有事務機
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Printer>>> GetAll(
        [FromQuery] string? keyword,
        [FromQuery] int? partnerId,
        [FromQuery] bool? isActive,
        [FromQuery] bool? unassigned,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _context.Printers
            .Include(p => p.Partner)
            .Include(p => p.Model)
            .ThenInclude(m => m!.Brand)
            .AsQueryable();

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(p =>
                p.Name.Contains(keyword) ||
                p.Code.Contains(keyword) ||
                (p.SerialNumber != null && p.SerialNumber.Contains(keyword)));
        }

        if (partnerId.HasValue)
        {
            query = query.Where(p => p.PartnerId == partnerId.Value);
        }

        // 庫存機（未指派客戶）— printer_setup 換機挑選新機用
        if (unassigned == true)
        {
            query = query.Where(p => p.PartnerId == null);
        }

        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        Response.Headers.Append("X-Total-Count", total.ToString());
        return Ok(items);
    }

    /// <summary>
    /// 取得單一事務機
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Printer>> GetById(int id)
    {
        var printer = await _context.Printers
            .Include(p => p.Partner)
            .Include(p => p.Model)
            .ThenInclude(m => m!.Brand)
            .Include(p => p.PrintRecords.OrderByDescending(r => r.Date).Take(10))
            .Include(p => p.AlertRecords.Where(a => a.State != "resolved"))
            .FirstOrDefaultAsync(p => p.Id == id);

        if (printer == null)
            return NotFound();

        return Ok(printer);
    }

    /// <summary>
    /// 建立事務機
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Printer>> Create([FromBody] Printer printer)
    {
        printer.CreatedAt = DateTime.UtcNow;
        printer.UpdatedAt = DateTime.UtcNow;

        _context.Printers.Add(printer);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = printer.Id }, printer);
    }

    /// <summary>
    /// 更新事務機
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Printer printer)
    {
        if (id != printer.Id)
            return BadRequest();

        var existing = await _context.Printers.FindAsync(id);
        if (existing == null)
            return NotFound();

        existing.Code = printer.Code;
        existing.Name = printer.Name;
        existing.PartnerId = printer.PartnerId;
        existing.PartnerNumber = printer.PartnerNumber;
        existing.ModelId = printer.ModelId;
        existing.Description = printer.Description;
        existing.PrinterName = printer.PrinterName;
        existing.SerialNumber = printer.SerialNumber;
        existing.Ip = printer.Ip;
        existing.Mac = printer.Mac;
        existing.IsActive = printer.IsActive;
        existing.UserId = printer.UserId;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// 刪除事務機
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var printer = await _context.Printers.FindAsync(id);
        if (printer == null)
            return NotFound();

        _context.Printers.Remove(printer);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// 切換啟用狀態
    /// </summary>
    [HttpPatch("{id}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var printer = await _context.Printers.FindAsync(id);
        if (printer == null)
            return NotFound();

        printer.IsActive = !printer.IsActive;
        printer.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { printer.Id, printer.IsActive });
    }

    /// <summary>
    /// 取得事務機的抄表記錄
    /// </summary>
    [HttpGet("{id}/records")]
    public async Task<ActionResult<IEnumerable<PrintRecord>>> GetRecords(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        var printer = await _context.Printers.FindAsync(id);
        if (printer == null)
            return NotFound();

        var query = _context.PrintRecords
            .Where(r => r.PrinterId == id)
            .OrderByDescending(r => r.Date);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        Response.Headers.Append("X-Total-Count", total.ToString());
        return Ok(items);
    }

    /// <summary>
    /// 取得事務機的告警記錄
    /// </summary>
    [HttpGet("{id}/alerts")]
    public async Task<ActionResult<IEnumerable<AlertRecord>>> GetAlerts(
        int id,
        [FromQuery] string? state,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        var printer = await _context.Printers.FindAsync(id);
        if (printer == null)
            return NotFound();

        var query = _context.AlertRecords
            .Where(a => a.PrinterId == id);

        if (!string.IsNullOrEmpty(state))
        {
            query = query.Where(a => a.State == state);
        }

        query = query.OrderByDescending(a => a.CreatedAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        Response.Headers.Append("X-Total-Count", total.ToString());
        return Ok(items);
    }

    /// <summary>
    /// 取得事務機的計費設定
    /// </summary>
    [HttpGet("{id}/billing")]
    public async Task<ActionResult> GetBillingConfig(int id)
    {
        var config = await _context.PrinterBillingConfigs
            .FirstOrDefaultAsync(c => c.PrinterId == id);

        if (config == null)
            return Ok(new { hasBilling = false });

        // 讀取張數類型單價（新系統）
        var sheetPrices = await _context.PrinterBillingSheetPrices
            .Include(p => p.SheetType)
            .Where(p => p.PrinterId == id && p.SheetType!.IsActive)
            .OrderBy(p => p.SheetType!.SortOrder)
            .ToListAsync();

        var sheetTypes = sheetPrices.Select(p => new
        {
            id = p.SheetTypeId,
            name = p.SheetType!.Name,
            unitPrice = p.UnitPrice
        }).ToList();

        return Ok(new
        {
            hasBilling = true,
            monthlyFee = config.MonthlyFee,
            pricePerBlack = config.PricePerBlack,
            pricePerColor = config.PricePerColor,
            pricePerLarge = config.PricePerLarge,
            freeBlackPages = config.FreeBlackPages,
            freeColorPages = config.FreeColorPages,
            freeLargePages = config.FreeLargePages,
            hasSheetTypes = sheetTypes.Any(),
            sheetTypes = sheetTypes
        });
    }

    #region 換機 / 退機 / 使用紀錄

    /// <summary>
    /// 換機：舊機回庫存，新機（NewPrinterId 從庫存挑選，或 NewPrinter 現場新建）接手客戶、合約與計費設定
    /// </summary>
    [HttpPost("{id}/replace")]
    public async Task<ActionResult> Replace(int id, [FromBody] Models.ReplacePrinterRequest request)
    {
        try
        {
            var newPrinter = await _lifecycleService.ReplaceAsync(id, request);
            return Ok(new
            {
                success = true,
                message = $"換機完成：由「{newPrinter.Name}」({newPrinter.Code}) 接手",
                oldPrinterId = id,
                newPrinterId = newPrinter.Id,
                newPrinterCode = newPrinter.Code
            });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 指派：把庫存機指派給客戶並開使用紀錄（printer_setup 新增設備用）
    /// </summary>
    [HttpPost("{id}/assign")]
    public async Task<ActionResult> Assign(int id, [FromBody] Models.AssignPrinterRequest request)
    {
        try
        {
            var p = await _lifecycleService.AssignAsync(id, request);
            return Ok(new { success = true, message = $"「{p.Name}」已指派給客戶", printerId = p.Id, printerCode = p.Code });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 退機：關閉使用紀錄、機器回庫存
    /// </summary>
    [HttpPost("{id}/return")]
    public async Task<ActionResult> Return(int id, [FromBody] Models.ReturnPrinterRequest? request)
    {
        try
        {
            var p = await _lifecycleService.ReturnAsync(id, request ?? new Models.ReturnPrinterRequest());
            return Ok(new { success = true, message = $"退機完成：「{p.Name}」已回庫存", printerId = p.Id });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 取得事務機的客戶使用紀錄與已工作時長（天）
    /// </summary>
    [HttpGet("{id}/usage-records")]
    public async Task<ActionResult> GetUsageRecords(int id)
    {
        var printer = await _context.Printers.FindAsync(id);
        if (printer == null)
            return NotFound();

        var (records, totalDays) = await _lifecycleService.GetUsageAsync(id);
        return Ok(new
        {
            printerId = id,
            totalWorkedDays = totalDays,
            records = records.Select(u => new
            {
                u.Id,
                u.PartnerId,
                partnerName = u.Partner?.Name,
                startDate = u.StartDate.ToString("yyyy-MM-dd"),
                endDate = u.EndDate?.ToString("yyyy-MM-dd"),
                durationDays = u.DurationDays,
                endReason = u.EndReason,
                endReasonDisplay = u.EndReasonDisplay,
                replacementPrinterId = u.ReplacementPrinterId,
                replacementPrinterName = u.ReplacementPrinter?.Name,
                u.Note
            })
        });
    }

    #endregion
}

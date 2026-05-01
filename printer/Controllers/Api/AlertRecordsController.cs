using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;

namespace printer.Controllers.Api;

/// <summary>
/// 告警回傳紀錄 RESTful API
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class AlertRecordsController : ControllerBase
{
    private readonly PrinterDbContext _context;

    public AlertRecordsController(PrinterDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 取得所有告警記錄
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AlertRecord>>> GetAll(
        [FromQuery] int? printerId,
        [FromQuery] string? state,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        var query = _context.AlertRecords
            .Include(a => a.Printer)
            .AsQueryable();

        if (printerId.HasValue)
        {
            query = query.Where(a => a.PrinterId == printerId.Value);
        }

        if (!string.IsNullOrEmpty(state))
        {
            query = query.Where(a => a.State == state);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        Response.Headers.Append("X-Total-Count", total.ToString());
        return Ok(items);
    }

    /// <summary>
    /// 取得單一告警記錄
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<AlertRecord>> GetById(int id)
    {
        var alert = await _context.AlertRecords
            .Include(a => a.Printer)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (alert == null)
            return NotFound();

        return Ok(alert);
    }

    /// <summary>
    /// 建立告警記錄
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AlertRecord>> Create([FromBody] AlertRecord alert)
    {
        alert.CreatedAt = DateTime.UtcNow;

        _context.AlertRecords.Add(alert);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = alert.Id }, alert);
    }

    /// <summary>
    /// 更新告警記錄
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] AlertRecord alert)
    {
        if (id != alert.Id)
            return BadRequest();

        var existing = await _context.AlertRecords.FindAsync(id);
        if (existing == null)
            return NotFound();

        existing.Code = alert.Code;
        existing.Message = alert.Message;
        existing.State = alert.State;
        if (alert.State == "resolved" && !existing.ResolvedAt.HasValue)
        {
            existing.ResolvedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// 刪除告警記錄
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var alert = await _context.AlertRecords.FindAsync(id);
        if (alert == null)
            return NotFound();

        _context.AlertRecords.Remove(alert);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// 標記告警為已解決
    /// </summary>
    [HttpPatch("{id}/resolve")]
    public async Task<IActionResult> Resolve(int id)
    {
        var alert = await _context.AlertRecords.FindAsync(id);
        if (alert == null)
            return NotFound();

        alert.State = "resolved";
        alert.ResolvedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { alert.Id, alert.State, alert.ResolvedAt });
    }

    /// <summary>
    /// 批次標記告警為已解決
    /// </summary>
    [HttpPatch("resolve-batch")]
    public async Task<IActionResult> ResolveBatch([FromBody] int[] ids)
    {
        var alerts = await _context.AlertRecords
            .Where(a => ids.Contains(a.Id))
            .ToListAsync();

        foreach (var alert in alerts)
        {
            alert.State = "resolved";
            alert.ResolvedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(new { ResolvedCount = alerts.Count });
    }
}

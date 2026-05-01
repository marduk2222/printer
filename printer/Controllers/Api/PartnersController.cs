using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;

namespace printer.Controllers.Api;

/// <summary>
/// 客戶 RESTful API
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class PartnersController : ControllerBase
{
    private readonly PrinterDbContext _context;

    public PartnersController(PrinterDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 取得所有客戶
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Partner>>> GetAll(
        [FromQuery] string? keyword,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
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
    /// 取得單一客戶
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Partner>> GetById(int id)
    {
        var partner = await _context.Partners
            .Include(p => p.Printers)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (partner == null)
            return NotFound();

        return Ok(partner);
    }

    /// <summary>
    /// 建立客戶
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Partner>> Create([FromBody] Partner partner)
    {
        partner.CreatedAt = DateTime.UtcNow;
        partner.UpdatedAt = DateTime.UtcNow;

        _context.Partners.Add(partner);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = partner.Id }, partner);
    }

    /// <summary>
    /// 更新客戶
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Partner partner)
    {
        if (id != partner.Id)
            return BadRequest();

        var existing = await _context.Partners.FindAsync(id);
        if (existing == null)
            return NotFound();

        existing.Code = partner.Code;
        existing.PartnerNumber = partner.PartnerNumber;
        existing.Name = partner.Name;
        existing.IsCompany = partner.IsCompany;
        existing.Vat = partner.Vat;
        existing.Phone = partner.Phone;
        existing.Mobile = partner.Mobile;
        existing.Email = partner.Email;
        existing.Address = partner.Address;
        existing.ContactName = partner.ContactName;
        existing.Note = partner.Note;
        existing.IsActive = partner.IsActive;
        existing.State = partner.State;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// 刪除客戶
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var partner = await _context.Partners.FindAsync(id);
        if (partner == null)
            return NotFound();

        _context.Partners.Remove(partner);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// 切換啟用狀態
    /// </summary>
    [HttpPatch("{id}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var partner = await _context.Partners.FindAsync(id);
        if (partner == null)
            return NotFound();

        partner.IsActive = !partner.IsActive;
        partner.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { partner.Id, partner.IsActive });
    }
}

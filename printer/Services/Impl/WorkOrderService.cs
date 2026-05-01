using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;

namespace printer.Services.Impl;

/// <summary>
/// 派工單服務實作
/// </summary>
public class WorkOrderService : IWorkOrderService
{
    private readonly PrinterDbContext _context;
    private readonly IWebHostEnvironment _env;

    public WorkOrderService(PrinterDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    #region 派工單 CRUD

    public async Task<List<WorkOrder>> GetListAsync(
        int? partnerId = null, int? printerId = null, string? status = null,
        DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        var query = _context.WorkOrders
            .Include(w => w.Partner)
            .Include(w => w.CreatedBy)
            .Include(w => w.WorkOrderPrinters).ThenInclude(p => p.Printer)
            .Include(w => w.WorkOrderAssignees).ThenInclude(a => a.User)
            .AsQueryable();

        if (partnerId.HasValue)
            query = query.Where(w => w.PartnerId == partnerId.Value);
        if (printerId.HasValue)
            query = query.Where(w => w.WorkOrderPrinters.Any(p => p.PrinterId == printerId.Value));
        if (!string.IsNullOrEmpty(status))
            query = query.Where(w => w.Status == status);
        if (dateFrom.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(dateFrom.Value.Date, DateTimeKind.Local).ToUniversalTime();
            query = query.Where(w => w.CreatedAt >= fromUtc);
        }
        if (dateTo.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(dateTo.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
            query = query.Where(w => w.CreatedAt < toUtc);
        }

        return await query
            .OrderByDescending(w => w.ScheduledAt.HasValue)
            .ThenByDescending(w => w.ScheduledAt)
            .ThenByDescending(w => w.CreatedAt)
            .ToListAsync();
    }

    public async Task<WorkOrder?> GetAsync(int id)
    {
        return await _context.WorkOrders
            .Include(w => w.Partner)
            .Include(w => w.CreatedBy)
            .Include(w => w.WorkOrderPrinters).ThenInclude(p => p.Printer).ThenInclude(p => p!.Partner)
            .Include(w => w.WorkOrderAssignees).ThenInclude(a => a.User)
            .Include(w => w.Images)
            .FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task<WorkOrder> CreateAsync(WorkOrder workOrder, List<int>? printerIds = null, List<int>? userIds = null)
    {
        workOrder.OrderNumber = await GenerateOrderNumberAsync();
        workOrder.CreatedAt = DateTime.UtcNow;
        workOrder.UpdatedAt = DateTime.UtcNow;
        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync();

        await SyncPrintersAsync(workOrder.Id, printerIds ?? new List<int>());
        await SyncAssigneesAsync(workOrder.Id, userIds ?? new List<int>());

        return workOrder;
    }

    public async Task<WorkOrder> UpdateAsync(WorkOrder workOrder, List<int>? printerIds = null, List<int>? userIds = null)
    {
        var existing = await _context.WorkOrders.FindAsync(workOrder.Id)
            ?? throw new ArgumentException("找不到指定的派工單");

        existing.PartnerId = workOrder.PartnerId;
        existing.Title = workOrder.Title;
        existing.Description = workOrder.Description;
        existing.ScheduledAt = workOrder.ScheduledAt;
        existing.ExpectedDays = workOrder.ExpectedDays;
        existing.Note = workOrder.Note;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        if (printerIds != null)
            await SyncPrintersAsync(existing.Id, printerIds);
        if (userIds != null)
            await SyncAssigneesAsync(existing.Id, userIds);

        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var workOrder = await _context.WorkOrders
            .Include(w => w.Images)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (workOrder == null) return false;

        foreach (var img in workOrder.Images)
            DeleteImageFile(img.FilePath);

        _context.WorkOrders.Remove(workOrder);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateStatusAsync(int id, string status, string? note = null)
    {
        var workOrder = await _context.WorkOrders.FindAsync(id);
        if (workOrder == null) return false;

        workOrder.Status = status;
        if (status == "completed")
            workOrder.CompletedAt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(note))
            workOrder.Note = note;
        workOrder.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    #endregion

    #region 多對多同步

    private async Task SyncPrintersAsync(int workOrderId, List<int> printerIds)
    {
        var existing = await _context.WorkOrderPrinters
            .Where(p => p.WorkOrderId == workOrderId)
            .ToListAsync();
        _context.WorkOrderPrinters.RemoveRange(existing);

        foreach (var pid in printerIds.Distinct())
        {
            _context.WorkOrderPrinters.Add(new WorkOrderPrinter
            {
                WorkOrderId = workOrderId,
                PrinterId = pid
            });
        }
        await _context.SaveChangesAsync();
    }

    private async Task SyncAssigneesAsync(int workOrderId, List<int> userIds)
    {
        var existing = await _context.WorkOrderAssignees
            .Where(a => a.WorkOrderId == workOrderId)
            .ToListAsync();
        _context.WorkOrderAssignees.RemoveRange(existing);

        foreach (var uid in userIds.Distinct())
        {
            _context.WorkOrderAssignees.Add(new WorkOrderAssignee
            {
                WorkOrderId = workOrderId,
                UserId = uid
            });
        }
        await _context.SaveChangesAsync();
    }

    #endregion

    #region 圖片管理

    public async Task<WorkOrderImage> AddImageAsync(int workOrderId, IFormFile file, string? caption = null)
    {
        _ = await _context.WorkOrders.FindAsync(workOrderId)
            ?? throw new ArgumentException("找不到指定的派工單");

        var folder = Path.Combine(_env.WebRootPath, "uploads", "work-orders", workOrderId.ToString());
        Directory.CreateDirectory(folder);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var safeExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
        if (!safeExts.Contains(ext))
            throw new ArgumentException("不支援的圖片格式");

        var fileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(folder, fileName);
        var relativePath = $"/uploads/work-orders/{workOrderId}/{fileName}";

        using (var stream = new FileStream(fullPath, FileMode.Create))
            await file.CopyToAsync(stream);

        var image = new WorkOrderImage
        {
            WorkOrderId = workOrderId,
            FileName = Path.GetFileName(file.FileName),
            FilePath = relativePath,
            Caption = caption,
            UploadedAt = DateTime.UtcNow
        };

        _context.WorkOrderImages.Add(image);
        await _context.SaveChangesAsync();
        return image;
    }

    public async Task<bool> DeleteImageAsync(int imageId)
    {
        var image = await _context.WorkOrderImages.FindAsync(imageId);
        if (image == null) return false;

        DeleteImageFile(image.FilePath);
        _context.WorkOrderImages.Remove(image);
        await _context.SaveChangesAsync();
        return true;
    }

    private void DeleteImageFile(string relativePath)
    {
        try
        {
            var fullPath = Path.Combine(_env.WebRootPath,
                relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch { }
    }

    #endregion

    #region 派工單號

    public async Task<string> GenerateOrderNumberAsync()
    {
        var period = DateTime.UtcNow.ToString("yyyyMM");
        var count = await _context.WorkOrders
            .CountAsync(w => w.OrderNumber.StartsWith($"WO-{period}"));
        return $"WO-{period}-{(count + 1):D3}";
    }

    #endregion

    #region 張數互換比例

    public async Task<List<SheetTypeConversionRate>> GetConversionRatesAsync()
    {
        return await _context.SheetTypeConversionRates
            .Include(r => r.FromSheetType)
            .Include(r => r.ToSheetType)
            .OrderBy(r => r.FromSheetType!.SortOrder)
            .ThenBy(r => r.ToSheetType!.SortOrder)
            .ToListAsync();
    }

    public async Task<SheetTypeConversionRate?> GetConversionRateAsync(int id)
    {
        return await _context.SheetTypeConversionRates
            .Include(r => r.FromSheetType)
            .Include(r => r.ToSheetType)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<SheetTypeConversionRate> SaveConversionRateAsync(SheetTypeConversionRate rate)
    {
        if (rate.Id == 0)
        {
            rate.CreatedAt = DateTime.UtcNow;
            rate.UpdatedAt = DateTime.UtcNow;
            _context.SheetTypeConversionRates.Add(rate);
        }
        else
        {
            var existing = await _context.SheetTypeConversionRates.FindAsync(rate.Id)
                ?? throw new ArgumentException("找不到指定的互換比例設定");

            existing.FromSheetTypeId = rate.FromSheetTypeId;
            existing.ToSheetTypeId = rate.ToSheetTypeId;
            existing.Ratio = rate.Ratio;
            existing.IsActive = rate.IsActive;
            existing.Note = rate.Note;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return rate;
    }

    public async Task<bool> DeleteConversionRateAsync(int id)
    {
        var rate = await _context.SheetTypeConversionRates.FindAsync(id);
        if (rate == null) return false;

        _context.SheetTypeConversionRates.Remove(rate);
        await _context.SaveChangesAsync();
        return true;
    }

    #endregion
}

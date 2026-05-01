using printer.Data.Entities;

namespace printer.Services;

/// <summary>
/// 派工單服務介面
/// </summary>
public interface IWorkOrderService
{
    Task<List<WorkOrder>> GetListAsync(int? partnerId = null, int? printerId = null, string? status = null, DateTime? dateFrom = null, DateTime? dateTo = null);
    Task<WorkOrder?> GetAsync(int id);
    Task<WorkOrder> CreateAsync(WorkOrder workOrder, List<int>? printerIds = null, List<int>? userIds = null);
    Task<WorkOrder> UpdateAsync(WorkOrder workOrder, List<int>? printerIds = null, List<int>? userIds = null);
    Task<bool> DeleteAsync(int id);
    Task<bool> UpdateStatusAsync(int id, string status, string? note = null);
    Task<WorkOrderImage> AddImageAsync(int workOrderId, IFormFile file, string? caption = null);
    Task<bool> DeleteImageAsync(int imageId);
    Task<string> GenerateOrderNumberAsync();

    // 張數互換比例管理
    Task<List<SheetTypeConversionRate>> GetConversionRatesAsync();
    Task<SheetTypeConversionRate?> GetConversionRateAsync(int id);
    Task<SheetTypeConversionRate> SaveConversionRateAsync(SheetTypeConversionRate rate);
    Task<bool> DeleteConversionRateAsync(int id);
}

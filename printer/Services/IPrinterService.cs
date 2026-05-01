using printer.Models.Dto;

namespace printer.Services;

/// <summary>
/// 印表機服務介面
/// </summary>
public interface IPrinterService
{
    /// <summary>
    /// 取得印表機列表
    /// </summary>
    Task<List<PrinterDataDto>> GetPrintersAsync(PrinterListRequest request);

    /// <summary>
    /// 取得定期同步印表機列表
    /// </summary>
    Task<List<PrinterDataDto>> GetPeriodPrintersAsync(PeriodRequest request);

    /// <summary>
    /// 更新耗材資訊
    /// </summary>
    Task<SuppliesUpdateResponse?> UpdateSuppliesAsync(SuppliesUpdateRequest request);

    /// <summary>
    /// 更新警報
    /// </summary>
    Task<AlertsUpdateResponse?> UpdateAlertsAsync(AlertsUpdateRequest request);

    /// <summary>
    /// 寫入抄表記錄
    /// </summary>
    Task<(RecordResponse? response, string? error)> WriteRecordAsync(RecordRequest request);

    /// <summary>
    /// 更新設備資訊
    /// </summary>
    Task<DeviceUpdateResponse?> UpdateDeviceAsync(DeviceUpdateRequest request);

    /// <summary>
    /// 建立安裝記錄
    /// </summary>
    Task<(InstallResponse? response, string? error)> CreateInstallAsync(InstallRequest request);
}

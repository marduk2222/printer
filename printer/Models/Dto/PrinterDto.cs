namespace printer.Models.Dto;

#region Printer Data

/// <summary>
/// 印表機資料
/// </summary>
public class PrinterDataDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int? PartnerId { get; set; }
    public int? UserId { get; set; }
    public int ModelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Number { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public string Mac { get; set; } = string.Empty;
    public string PrinterName { get; set; } = string.Empty;
}

/// <summary>
/// 印表機列表請求
/// </summary>
public class PrinterListRequest
{
    public string? PartnerId { get; set; }
    public int? UserId { get; set; }
}

/// <summary>
/// 週期同步請求
/// </summary>
public class PeriodRequest
{
    public string? PartnerId { get; set; }
}

#endregion

#region Supplies

/// <summary>
/// 耗材更新請求
/// </summary>
public class SuppliesUpdateRequest
{
    public string Code { get; set; } = string.Empty;
    public float? TonerBlack { get; set; }
    public float? TonerCyan { get; set; }
    public float? TonerMagenta { get; set; }
    public float? TonerYellow { get; set; }
    public float? TonerWaste { get; set; }
    public float? DrumBlack { get; set; }
    public float? DrumCyan { get; set; }
    public float? DrumMagenta { get; set; }
    public float? DrumYellow { get; set; }
}

/// <summary>
/// 耗材更新回應
/// </summary>
public class SuppliesUpdateResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
}

#endregion

#region Alerts

/// <summary>
/// 警報更新請求
/// </summary>
public class AlertsUpdateRequest
{
    public string Code { get; set; } = string.Empty;
    public List<int> Alerts { get; set; } = new();
}

/// <summary>
/// 警報更新回應
/// </summary>
public class AlertsUpdateResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int Count { get; set; }
}

#endregion

#region Record

/// <summary>
/// 抄表記錄請求
/// </summary>
public class RecordRequest
{
    public string Code { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public int? BlackPrint { get; set; }
    public int? ColorPrint { get; set; }
    public int? LargePrint { get; set; }
}

/// <summary>
/// 抄表記錄回應
/// </summary>
public class RecordResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int RecordId { get; set; }
}

#endregion

#region Device

/// <summary>
/// 設備更新請求
/// </summary>
public class DeviceUpdateRequest
{
    public string Code { get; set; } = string.Empty;
    public string? Mac { get; set; }
    public string? Ip { get; set; }
    public string? SerialNumber { get; set; }
    public string? PrinterName { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// 設備更新回應
/// </summary>
public class DeviceUpdateResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
}

#endregion

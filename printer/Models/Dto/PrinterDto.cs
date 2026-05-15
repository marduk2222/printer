namespace printer.Models.Dto;

#region Printer Data

/// <summary>
/// 印表機資料（API 回傳給 printer_info）
/// Brand / Model 為 Brand.Code / PrinterModel.Code 字串，供 OID dispatch 使用。
/// </summary>
public class PrinterDataDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int? PartnerId { get; set; }
    public int? UserId { get; set; }

    /// <summary>廠牌代碼（如 ricoh / xerox / toshiba / kyocera / common）</summary>
    public string Brand { get; set; } = string.Empty;

    /// <summary>具體型號代碼（如 2555c / 3515ac，廠牌內細分用）</summary>
    public string Model { get; set; } = string.Empty;

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
/// 抄表計數明細項目（四維格式）
/// driver_key 編碼為 "{output}.{category}.{color}.{size}"
/// </summary>
public class CounterItemDto
{
    /// <summary>列印 / 掃描：printed / scanned</summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>類別：print / copy / fax / network / list / scan</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>色彩：color_full / duotone / mono / black</summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>紙張大小：large / normal</summary>
    public string Size { get; set; } = string.Empty;

    public int Sheets { get; set; }

    /// <summary>組合 driver_key（output.category.color.size）</summary>
    public string ToDriverKey() =>
        $"{(Output ?? "").ToLowerInvariant()}.{(Category ?? "").ToLowerInvariant()}.{(Color ?? "").ToLowerInvariant()}.{(Size ?? "").ToLowerInvariant()}";
}

/// <summary>
/// 抄表記錄請求。items 有值時優先以 items 落到 PrintRecordValue；
/// 若 items 為空則仍接受舊格式 BlackPrint / ColorPrint / LargePrint。
/// </summary>
public class RecordRequest
{
    public string Code { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;

    /// <summary>0=一般, 1=起表, 2=尾表, 3=換機</summary>
    public int State { get; set; }

    /// <summary>SNMP 原始 JSON（除錯用）</summary>
    public string? Data { get; set; }

    /// <summary>四維計數明細</summary>
    public List<CounterItemDto>? Items { get; set; }

    // 舊格式（向下相容）
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

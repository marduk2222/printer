namespace printer.Models;

/// <summary>
/// 換機請求（網頁表單與 REST API 共用）
/// </summary>
public class ReplacePrinterRequest
{
    /// <summary>
    /// 換機日期（未填以今日計）
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// 接手的新機 ID（從庫存挑選；與 NewPrinter 擇一）
    /// </summary>
    public int? NewPrinterId { get; set; }

    /// <summary>
    /// 現場新建的新機資料（與 NewPrinterId 擇一）
    /// </summary>
    public NewPrinterData? NewPrinter { get; set; }

    /// <summary>
    /// 是否將舊機計費設定（月租、各張數類型單價/贈送/誤印率/互換、階梯）複製到新機，預設 true
    /// </summary>
    public bool CopyBilling { get; set; } = true;

    public string? Note { get; set; }

    /// <summary>
    /// 補登舊機尾表（選填）：SheetTypeId → 張數。
    /// 舊機已拆除無法抄表時由人工輸入，會以換機日建立一筆尾表抄表記錄（state=2）。
    /// </summary>
    public Dictionary<int, int>? FinalMeter { get; set; }
}

/// <summary>
/// 換機時現場新建機器的資料
/// </summary>
public class NewPrinterData
{
    /// <summary>未填則自動產生 yyyyMM + 流水號</summary>
    public string? Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? BrandId { get; set; }

    public int? ModelId { get; set; }

    public string? SerialNumber { get; set; }

    public string? Ip { get; set; }

    public string? Mac { get; set; }

    public string? PrinterName { get; set; }

    public string? Description { get; set; }
}

/// <summary>
/// 退機請求（網頁表單與 REST API 共用）
/// </summary>
public class ReturnPrinterRequest
{
    /// <summary>
    /// 退機日期（未填以今日計）
    /// </summary>
    public DateOnly? Date { get; set; }

    public string? Note { get; set; }

    /// <summary>
    /// 補登尾表（選填）：SheetTypeId → 張數。
    /// 已事先補登抄表記錄者留空；機器已拆除無法抄表時由人工輸入，會以退機日建立尾表抄表記錄（state=2）。
    /// </summary>
    public Dictionary<int, int>? FinalMeter { get; set; }
}

/// <summary>
/// 指派請求：把庫存機指派給客戶（printer_setup 新增設備用）
/// </summary>
public class AssignPrinterRequest
{
    public int PartnerId { get; set; }

    /// <summary>
    /// 裝機日期（未填以今日計）
    /// </summary>
    public DateOnly? Date { get; set; }

    public string? Note { get; set; }
}

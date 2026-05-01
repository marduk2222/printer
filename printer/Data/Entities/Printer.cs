using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 事務機
/// </summary>
[Table("printers")]
public class Printer
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 事務機代碼 (主要識別欄位)
    /// </summary>
    [Column("code")]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 事務機名稱
    /// </summary>
    [Column("name")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 客戶 ID
    /// </summary>
    [Column("partner_id")]
    public int? PartnerId { get; set; }

    /// <summary>
    /// 客戶編號 (顯示用)
    /// </summary>
    [Column("partner_number")]
    [MaxLength(50)]
    public string? PartnerNumber { get; set; }

    /// <summary>
    /// 型號 ID
    /// </summary>
    [Column("model_id")]
    public int? ModelId { get; set; }

    /// <summary>
    /// 說明
    /// </summary>
    [Column("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// 印表機顯示名稱
    /// </summary>
    [Column("printer_name")]
    [MaxLength(200)]
    public string? PrinterName { get; set; }

    /// <summary>
    /// 序號
    /// </summary>
    [Column("serial_number")]
    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    /// <summary>
    /// IP 位址
    /// </summary>
    [Column("ip")]
    [MaxLength(50)]
    public string? Ip { get; set; }

    /// <summary>
    /// MAC 位址
    /// </summary>
    [Column("mac")]
    [MaxLength(50)]
    public string? Mac { get; set; }

    /// <summary>
    /// 是否啟用
    /// </summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 客戶詳情頁中此事務機的顯示排序（由小到大；可拖曳調整）
    /// </summary>
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// 在計費群組頁面內的顯示排序（成員月租明細 / 群組成員 共用，可拖曳調整）
    /// </summary>
    [Column("billing_group_sort_order")]
    public int BillingGroupSortOrder { get; set; } = 0;

    #region 耗材資訊

    /// <summary>
    /// 黑色碳粉 (%)
    /// </summary>
    [Column("toner_black")]
    public float? TonerBlack { get; set; }

    /// <summary>
    /// 青色碳粉 (%)
    /// </summary>
    [Column("toner_cyan")]
    public float? TonerCyan { get; set; }

    /// <summary>
    /// 洋紅碳粉 (%)
    /// </summary>
    [Column("toner_magenta")]
    public float? TonerMagenta { get; set; }

    /// <summary>
    /// 黃色碳粉 (%)
    /// </summary>
    [Column("toner_yellow")]
    public float? TonerYellow { get; set; }

    /// <summary>
    /// 廢粉 (%)
    /// </summary>
    [Column("toner_waste")]
    public float? TonerWaste { get; set; }

    /// <summary>
    /// 黑色感光鼓 (%)
    /// </summary>
    [Column("drum_black")]
    public float? DrumBlack { get; set; }

    /// <summary>
    /// 青色感光鼓 (%)
    /// </summary>
    [Column("drum_cyan")]
    public float? DrumCyan { get; set; }

    /// <summary>
    /// 洋紅感光鼓 (%)
    /// </summary>
    [Column("drum_magenta")]
    public float? DrumMagenta { get; set; }

    /// <summary>
    /// 黃色感光鼓 (%)
    /// </summary>
    [Column("drum_yellow")]
    public float? DrumYellow { get; set; }

    #endregion

    #region 耗材警告閾值

    /// <summary>
    /// 黑色碳粉警告閾值 (%)，預設 20%
    /// </summary>
    [Column("toner_black_alert_threshold")]
    public int TonerBlackAlertThreshold { get; set; } = 20;

    /// <summary>
    /// 青色碳粉警告閾值 (%)，預設 20%
    /// </summary>
    [Column("toner_cyan_alert_threshold")]
    public int TonerCyanAlertThreshold { get; set; } = 20;

    /// <summary>
    /// 洋紅碳粉警告閾值 (%)，預設 20%
    /// </summary>
    [Column("toner_magenta_alert_threshold")]
    public int TonerMagentaAlertThreshold { get; set; } = 20;

    /// <summary>
    /// 黃色碳粉警告閾值 (%)，預設 20%
    /// </summary>
    [Column("toner_yellow_alert_threshold")]
    public int TonerYellowAlertThreshold { get; set; } = 20;

    /// <summary>
    /// 黑色感光鼓警告閾值 (%)，預設 20%
    /// </summary>
    [Column("drum_black_alert_threshold")]
    public int DrumBlackAlertThreshold { get; set; } = 20;

    /// <summary>
    /// 青色感光鼓警告閾值 (%)，預設 20%
    /// </summary>
    [Column("drum_cyan_alert_threshold")]
    public int DrumCyanAlertThreshold { get; set; } = 20;

    /// <summary>
    /// 洋紅感光鼓警告閾值 (%)，預設 20%
    /// </summary>
    [Column("drum_magenta_alert_threshold")]
    public int DrumMagentaAlertThreshold { get; set; } = 20;

    /// <summary>
    /// 黃色感光鼓警告閾值 (%)，預設 20%
    /// </summary>
    [Column("drum_yellow_alert_threshold")]
    public int DrumYellowAlertThreshold { get; set; } = 20;

    #endregion

    /// <summary>
    /// 檢查是否有任何碳粉低於警告閾值
    /// </summary>
    [NotMapped]
    public bool HasLowToner =>
        (TonerBlack.HasValue && TonerBlack < TonerBlackAlertThreshold) ||
        (TonerCyan.HasValue && TonerCyan < TonerCyanAlertThreshold) ||
        (TonerMagenta.HasValue && TonerMagenta < TonerMagentaAlertThreshold) ||
        (TonerYellow.HasValue && TonerYellow < TonerYellowAlertThreshold);

    /// <summary>
    /// 檢查是否有任何感光鼓低於警告閾值
    /// </summary>
    [NotMapped]
    public bool HasLowDrum =>
        (DrumBlack.HasValue && DrumBlack < DrumBlackAlertThreshold) ||
        (DrumCyan.HasValue && DrumCyan < DrumCyanAlertThreshold) ||
        (DrumMagenta.HasValue && DrumMagenta < DrumMagentaAlertThreshold) ||
        (DrumYellow.HasValue && DrumYellow < DrumYellowAlertThreshold);

    /// <summary>
    /// 檢查是否有任何耗材低於警告閾值
    /// </summary>
    [NotMapped]
    public bool HasLowSupply => HasLowToner || HasLowDrum;

    #region 合約資訊

    /// <summary>
    /// 合約開始日期
    /// </summary>
    [Column("contract_start_date")]
    public DateOnly? ContractStartDate { get; set; }

    /// <summary>
    /// 合約結束日期
    /// </summary>
    [Column("contract_end_date")]
    public DateOnly? ContractEndDate { get; set; }

    /// <summary>
    /// 押金
    /// </summary>
    [Column("deposit", TypeName = "decimal(10,2)")]
    public decimal? Deposit { get; set; }

    /// <summary>
    /// 合約到期前幾天通知
    /// </summary>
    [Column("contract_alert_days")]
    public int ContractAlertDays { get; set; } = 30;

    /// <summary>
    /// 合約是否即將到期
    /// </summary>
    [NotMapped]
    public bool IsContractExpiringSoon
    {
        get
        {
            if (!ContractEndDate.HasValue) return false;
            var today = DateOnly.FromDateTime(DateTime.Today);
            var daysLeft = ContractEndDate.Value.DayNumber - today.DayNumber;
            return daysLeft > 0 && daysLeft <= ContractAlertDays;
        }
    }

    /// <summary>
    /// 合約是否已到期
    /// </summary>
    [NotMapped]
    public bool IsContractExpired
    {
        get
        {
            if (!ContractEndDate.HasValue) return false;
            var today = DateOnly.FromDateTime(DateTime.Today);
            return ContractEndDate.Value < today;
        }
    }

    /// <summary>
    /// 合約狀態: none/normal/expiring/expired
    /// </summary>
    [NotMapped]
    public string ContractStatus
    {
        get
        {
            if (!ContractEndDate.HasValue) return "none";
            if (IsContractExpired) return "expired";
            if (IsContractExpiringSoon) return "expiring";
            return "normal";
        }
    }

    #endregion

    /// <summary>
    /// 廠牌 ID
    /// </summary>
    [Column("brand_id")]
    public int? BrandId { get; set; }

    /// <summary>
    /// 負責人員 ID
    /// </summary>
    [Column("user_id")]
    public int? UserId { get; set; }

    /// <summary>
    /// 計費群組 ID — 加入群組後，張數計費（單價/誤印率/贈送/階梯）以群組設定為準，月租仍由 PrinterBillingConfig 控制
    /// </summary>
    [Column("billing_group_id")]
    public int? BillingGroupId { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新時間
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("PartnerId")]
    public virtual Partner? Partner { get; set; }

    [ForeignKey("ModelId")]
    public virtual PrinterModel? Model { get; set; }

    [ForeignKey("BrandId")]
    public virtual Brand? Brand { get; set; }

    [ForeignKey("BillingGroupId")]
    public virtual PrinterBillingGroup? BillingGroup { get; set; }

    public virtual ICollection<PrintRecord> PrintRecords { get; set; } = new List<PrintRecord>();
    public virtual ICollection<AlertRecord> AlertRecords { get; set; } = new List<AlertRecord>();
    public virtual ICollection<PrinterMaintainer> Maintainers { get; set; } = new List<PrinterMaintainer>();
}

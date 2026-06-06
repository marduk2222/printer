using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 事務機客戶使用紀錄 — 記錄機器曾被哪位客戶使用的期間。
/// EndDate 為 null 表示使用中；換機/退機/客戶異動時關閉紀錄並寫入結束原因。
/// 已工作時長 = 各紀錄時長（天）加總（使用中以今日計）。
/// </summary>
[Table("printer_usage_records")]
public class PrinterUsageRecord
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("printer_id")]
    public int PrinterId { get; set; }

    [Column("partner_id")]
    public int PartnerId { get; set; }

    /// <summary>
    /// 裝機（開始使用）日期
    /// </summary>
    [Column("start_date")]
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// 結束使用日期（null = 使用中）
    /// </summary>
    [Column("end_date")]
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// 結束原因：replace=換機, return=退機, transfer=客戶異動
    /// </summary>
    [Column("end_reason")]
    [MaxLength(20)]
    public string? EndReason { get; set; }

    /// <summary>
    /// 換機時接手的新機 ID（EndReason=replace 時填）
    /// </summary>
    [Column("replacement_printer_id")]
    public int? ReplacementPrinterId { get; set; }

    [Column("note")]
    [MaxLength(500)]
    public string? Note { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 使用時長（天）；使用中以今日計
    /// </summary>
    [NotMapped]
    public int DurationDays
    {
        get
        {
            var end = EndDate ?? DateOnly.FromDateTime(DateTime.Today);
            return Math.Max(0, end.DayNumber - StartDate.DayNumber);
        }
    }

    /// <summary>
    /// 結束原因顯示名稱
    /// </summary>
    [NotMapped]
    public string EndReasonDisplay => EndReason switch
    {
        "replace" => "換機",
        "return" => "退機",
        "transfer" => "客戶異動",
        _ => EndDate.HasValue ? "已結束" : "使用中"
    };

    // Navigation
    [ForeignKey("PrinterId")]
    public virtual Printer? Printer { get; set; }

    [ForeignKey("PartnerId")]
    public virtual Partner? Partner { get; set; }

    [ForeignKey("ReplacementPrinterId")]
    public virtual Printer? ReplacementPrinter { get; set; }
}

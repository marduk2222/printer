using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 張數回傳紀錄 (抄表紀錄)
/// </summary>
[Table("print_records")]
public class PrintRecord
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 事務機 ID
    /// </summary>
    [Column("printer_id")]
    public int PrinterId { get; set; }

    /// <summary>
    /// 客戶 ID
    /// </summary>
    [Column("partner_id")]
    public int? PartnerId { get; set; }

    /// <summary>
    /// 記錄日期
    /// </summary>
    [Column("date")]
    public DateOnly Date { get; set; }

    /// <summary>
    /// 黑白列印張數
    /// </summary>
    [Column("black_sheets")]
    public int BlackSheets { get; set; }

    /// <summary>
    /// 彩色列印張數
    /// </summary>
    [Column("color_sheets")]
    public int ColorSheets { get; set; }

    /// <summary>
    /// 大張列印張數
    /// </summary>
    [Column("large_sheets")]
    public int LargeSheets { get; set; }

    /// <summary>
    /// 狀態: auto=自動回傳, manual=手動輸入
    /// </summary>
    [Column("state")]
    [MaxLength(20)]
    public string State { get; set; } = "auto";

    /// <summary>
    /// 回傳次數 (同日多次回傳)
    /// </summary>
    [Column("count")]
    public int Count { get; set; } = 1;

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
    [ForeignKey("PrinterId")]
    public virtual Printer? Printer { get; set; }

    [ForeignKey("PartnerId")]
    public virtual Partner? Partner { get; set; }

    /// <summary>
    /// 各張數類型的值（對應 SheetType；新系統用此，舊資料則保留 BlackSheets/ColorSheets/LargeSheets 欄位）
    /// </summary>
    [InverseProperty(nameof(PrintRecordValue.Record))]
    public virtual ICollection<PrintRecordValue> Values { get; set; } = new List<PrintRecordValue>();
}

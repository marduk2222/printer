using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace printer.Data.Entities;

/// <summary>
/// 全域張數類型定義（可被多個機型共用）
/// </summary>
[Table("sheet_types")]
public class SheetType
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 顯示名稱，例如：黑白、彩色、彩色A3
    /// </summary>
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ICollection<SheetTypeKey> Keys { get; set; } = new List<SheetTypeKey>();

    public virtual ICollection<PrinterModelSheetType> PrinterModelSheetTypes { get; set; } = new List<PrinterModelSheetType>();

    public virtual ICollection<PrinterBillingSheetPrice> BillingPrices { get; set; } = new List<PrinterBillingSheetPrice>();

    public virtual ICollection<PrintRecordValue> RecordValues { get; set; } = new List<PrintRecordValue>();
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace printer.Data.Entities;

/// <summary>
/// 張數類型互換比例
/// 用途：當某類型有剩餘贈送張數時，可依比例折抵另一類型的計費張數
/// 例如：FromSheetType=黑白, ToSheetType=彩色, Ratio=10
///       表示 1 張彩色贈送可折抵 10 張黑白計費
/// </summary>
[Table("sheet_type_conversion_rates")]
public class SheetTypeConversionRate
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 被折抵的張數類型（例如：黑白）
    /// </summary>
    [Column("from_sheet_type_id")]
    public int FromSheetTypeId { get; set; }

    /// <summary>
    /// 用來折抵的贈送類型（例如：彩色）
    /// </summary>
    [Column("to_sheet_type_id")]
    public int ToSheetTypeId { get; set; }

    /// <summary>
    /// 折抵比例：1 個 ToSheetType 的贈送張數可折抵多少 FromSheetType 的張數
    /// 例如：Ratio = 10 表示 1 彩色贈送 = 10 黑白被折抵
    /// </summary>
    [Column("ratio")]
    [Precision(10, 4)]
    public decimal Ratio { get; set; } = 1;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("note")]
    [MaxLength(200)]
    public string? Note { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("FromSheetTypeId")]
    public virtual SheetType? FromSheetType { get; set; }

    [ForeignKey("ToSheetTypeId")]
    public virtual SheetType? ToSheetType { get; set; }
}

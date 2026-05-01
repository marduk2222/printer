using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace printer.Data.Entities;

/// <summary>
/// 計費模板各張數類型的計費單價
/// </summary>
[Table("billing_template_sheet_prices")]
public class BillingTemplateSheetPrice
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("template_id")]
    public int TemplateId { get; set; }

    [Column("sheet_type_id")]
    public int SheetTypeId { get; set; }

    [Column("unit_price")]
    [Precision(10, 4)]
    public decimal UnitPrice { get; set; } = 0;

    [Column("discount_percent")]
    [Precision(5, 2)]
    public decimal DiscountPercent { get; set; } = 0;

    [Column("free_pages")]
    public int FreePages { get; set; } = 0;

    /// <summary>
    /// 換算值：1 個基本單位等於幾張本類型紙（值越小越貴）。
    /// 例：彩色 1、大張 2、黑白 10 → 1 張彩色 = 2 張大張 = 10 張黑白。
    /// null 或 0 = 不參與互換。
    /// </summary>
    [Column("weight")]
    [Precision(10, 4)]
    public decimal? Weight { get; set; }

    /// <summary>
    /// 折抵順序（小者優先享用折抵）
    /// </summary>
    [Column("offset_order")]
    public int? OffsetOrder { get; set; }

    /// <summary>
    /// 顯示排序（由小到大；於 BillingTemplate Edit 拖曳調整）
    /// </summary>
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("TemplateId")]
    public virtual BillingTemplate? Template { get; set; }

    [ForeignKey("SheetTypeId")]
    public virtual SheetType? SheetType { get; set; }
}

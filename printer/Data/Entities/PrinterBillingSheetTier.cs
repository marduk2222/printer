using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace printer.Data.Entities;

/// <summary>
/// 張數類型的階梯計費設定
/// </summary>
[Table("printer_billing_sheet_tiers")]
public class PrinterBillingSheetTier
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("printer_id")]
    public int PrinterId { get; set; }

    [Column("sheet_type_id")]
    public int SheetTypeId { get; set; }

    [Column("tier_order")]
    public int TierOrder { get; set; }

    [Column("from_pages")]
    public int FromPages { get; set; }

    /// <summary>
    /// 結束張數（含），null 表示無上限
    /// </summary>
    [Column("to_pages")]
    public int? ToPages { get; set; }

    [Column("price")]
    [Precision(10, 4)]
    public decimal Price { get; set; } = 0;

    // Navigation
    [ForeignKey("PrinterId")]
    public virtual Printer? Printer { get; set; }

    [ForeignKey("SheetTypeId")]
    public virtual SheetType? SheetType { get; set; }
}

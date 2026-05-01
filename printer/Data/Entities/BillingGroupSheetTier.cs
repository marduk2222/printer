using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace printer.Data.Entities;

/// <summary>
/// 計費群組各張數類型的階梯計費（套用於合併張數後）
/// </summary>
[Table("billing_group_sheet_tiers")]
public class BillingGroupSheetTier
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("group_id")]
    public int GroupId { get; set; }

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

    [ForeignKey("GroupId")]
    public virtual PrinterBillingGroup? Group { get; set; }

    [ForeignKey("SheetTypeId")]
    public virtual SheetType? SheetType { get; set; }
}

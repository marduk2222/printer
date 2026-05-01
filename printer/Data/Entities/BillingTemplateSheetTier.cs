using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace printer.Data.Entities;

/// <summary>
/// 計費模板各張數類型的階梯計費設定
/// </summary>
[Table("billing_template_sheet_tiers")]
public class BillingTemplateSheetTier
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("template_id")]
    public int TemplateId { get; set; }

    [Column("sheet_type_id")]
    public int SheetTypeId { get; set; }

    [Column("tier_order")]
    public int TierOrder { get; set; }

    [Column("from_pages")]
    public int FromPages { get; set; }

    [Column("to_pages")]
    public int? ToPages { get; set; }

    [Column("price")]
    [Precision(10, 4)]
    public decimal Price { get; set; } = 0;

    [ForeignKey("TemplateId")]
    public virtual BillingTemplate? Template { get; set; }

    [ForeignKey("SheetTypeId")]
    public virtual SheetType? SheetType { get; set; }
}

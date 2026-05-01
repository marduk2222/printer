using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace printer.Data.Entities;

/// <summary>
/// 計費階梯設定 (階梯式計費用)
/// TierType: 0=黑白, 1=彩色, 2=大張
/// </summary>
[Table("billing_tiers")]
public class BillingTier
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("billing_config_id")]
    public int? BillingConfigId { get; set; }

    [Column("billing_template_id")]
    public int? BillingTemplateId { get; set; }

    /// <summary>
    /// 類型：0=黑白, 1=彩色, 2=大張
    /// </summary>
    [Column("tier_type")]
    public int TierType { get; set; } = 0;

    [Column("tier_order")]
    public int TierOrder { get; set; }

    [Column("from_pages")]
    public int FromPages { get; set; }

    /// <summary>
    /// 張數結束 (含，null 表示無上限)
    /// </summary>
    [Column("to_pages")]
    public int? ToPages { get; set; }

    [Column("price")]
    [Precision(10, 4)]
    public decimal Price { get; set; } = 0;

    // Navigation
    [ForeignKey("BillingConfigId")]
    public virtual PrinterBillingConfig? BillingConfig { get; set; }

    [ForeignKey("BillingTemplateId")]
    public virtual BillingTemplate? BillingTemplate { get; set; }
}

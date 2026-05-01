using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using printer.Data.Enums;

namespace printer.Data.Entities;

/// <summary>
/// 帳單明細
/// </summary>
[Table("invoice_items")]
public class InvoiceItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("invoice_id")]
    public int InvoiceId { get; set; }

    /// <summary>
    /// 事務機 ID — 群組合併列為 null
    /// </summary>
    [Column("printer_id")]
    public int? PrinterId { get; set; }

    /// <summary>
    /// 計費群組 ID — 群組合併列才有值
    /// </summary>
    [Column("billing_group_id")]
    public int? BillingGroupId { get; set; }

    /// <summary>
    /// 是否為群組合併張數列（true：合併張數費小計；false：單台月租或獨立計費列）
    /// </summary>
    [Column("is_group_summary")]
    public bool IsGroupSummary { get; set; } = false;

    /// <summary>
    /// 計費類型
    /// </summary>
    [Column("billing_type")]
    public BillingType BillingType { get; set; }

    /// <summary>
    /// 項目描述
    /// </summary>
    [Column("description")]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    #region 張數統計

    /// <summary>
    /// 黑白張數
    /// </summary>
    [Column("black_pages")]
    public int BlackPages { get; set; } = 0;

    /// <summary>
    /// 彩色張數
    /// </summary>
    [Column("color_pages")]
    public int ColorPages { get; set; } = 0;

    /// <summary>
    /// 大張張數
    /// </summary>
    [Column("large_pages")]
    public int LargePages { get; set; } = 0;

    #endregion

    /// <summary>
    /// 月租費
    /// </summary>
    [Column("monthly_fee")]
    [Precision(10, 2)]
    public decimal MonthlyFee { get; set; } = 0;

    /// <summary>
    /// 張數費用
    /// </summary>
    [Column("page_fee")]
    [Precision(10, 2)]
    public decimal PageFee { get; set; } = 0;

    /// <summary>
    /// 小計
    /// </summary>
    [Column("subtotal")]
    [Precision(10, 2)]
    public decimal Subtotal { get; set; } = 0;

    /// <summary>
    /// 張數類型計費明細（新系統）
    /// </summary>
    [Column("breakdown")]
    [MaxLength(1000)]
    public string? Breakdown { get; set; }

    // Navigation
    [ForeignKey("InvoiceId")]
    public virtual Invoice? Invoice { get; set; }

    [ForeignKey("PrinterId")]
    public virtual Printer? Printer { get; set; }

    [ForeignKey("BillingGroupId")]
    public virtual PrinterBillingGroup? BillingGroup { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using printer.Data.Enums;

namespace printer.Data.Entities;

/// <summary>
/// 事務機計費設定 (每台設備獨立設定)
/// </summary>
[Table("printer_billing_configs")]
public class PrinterBillingConfig : IBillingProfile
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("printer_id")]
    public int PrinterId { get; set; }

    [Column("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    #region 月租費用

    /// <summary>
    /// 月租費用
    /// </summary>
    [Column("monthly_fee")]
    [Precision(10, 2)]
    public decimal MonthlyFee { get; set; } = 0;

    #endregion

    #region 張數計費

    /// <summary>
    /// 張數計費方式
    /// </summary>
    [Column("page_method")]
    public PageMethod PageMethod { get; set; } = PageMethod.None;

    /// <summary>
    /// 黑白單價 (每張)
    /// </summary>
    [Column("price_per_black")]
    [Precision(10, 4)]
    public decimal PricePerBlack { get; set; } = 0;

    /// <summary>
    /// 彩色單價 (每張)
    /// </summary>
    [Column("price_per_color")]
    [Precision(10, 4)]
    public decimal PricePerColor { get; set; } = 0;

    /// <summary>
    /// 大張單價 (每張)
    /// </summary>
    [Column("price_per_large")]
    [Precision(10, 4)]
    public decimal PricePerLarge { get; set; } = 0;

    [Column("discount_percent_black")]
    [Precision(5, 2)]
    public decimal DiscountPercentBlack { get; set; } = 0;

    [Column("discount_percent_color")]
    [Precision(5, 2)]
    public decimal DiscountPercentColor { get; set; } = 0;

    [Column("discount_percent_large")]
    [Precision(5, 2)]
    public decimal DiscountPercentLarge { get; set; } = 0;

    #endregion

    #region 贈送/扣抵張數

    /// <summary>
    /// 免費黑白張數
    /// </summary>
    [Column("free_black_pages")]
    public int FreeBlackPages { get; set; } = 0;

    /// <summary>
    /// 免費彩色張數
    /// </summary>
    [Column("free_color_pages")]
    public int FreeColorPages { get; set; } = 0;

    /// <summary>
    /// 免費大張張數
    /// </summary>
    [Column("free_large_pages")]
    public int FreeLargePages { get; set; } = 0;

    #endregion

    #region 計費週期

    /// <summary>
    /// 月租計費週期（月數），1=每月, 3=每季, 6=半年, 12=每年
    /// </summary>
    [Column("monthly_fee_cycle")]
    public int MonthlyFeeCycle { get; set; } = 1;

    /// <summary>
    /// 張數計費週期（月數），1=每月, 3=每季, 6=半年, 12=每年
    /// </summary>
    [Column("page_fee_cycle")]
    public int PageFeeCycle { get; set; } = 1;

    #endregion

    /// <summary>
    /// 月租起算日期
    /// </summary>
    [Column("monthly_start_date")]
    public DateOnly? MonthlyStartDate { get; set; }

    /// <summary>
    /// 張數起算日期
    /// </summary>
    [Column("page_start_date")]
    public DateOnly? PageStartDate { get; set; }

    /// <summary>
    /// 月租目前作帳日期
    /// </summary>
    [Column("last_monthly_billed_date")]
    public DateOnly? LastMonthlyBilledDate { get; set; }

    /// <summary>
    /// 張數目前作帳日期
    /// </summary>
    [Column("last_page_billed_date")]
    public DateOnly? LastPageBilledDate { get; set; }

    /// <summary>
    /// 備註
    /// </summary>
    [Column("note")]
    [MaxLength(500)]
    public string? Note { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("PrinterId")]
    public virtual Printer? Printer { get; set; }

    /// <summary>
    /// 階梯式計費設定
    /// </summary>
    public virtual ICollection<BillingTier> Tiers { get; set; } = new List<BillingTier>();
}

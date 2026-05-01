using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using printer.Data.Enums;

namespace printer.Data.Entities;

/// <summary>
/// 計費模板 (預設費率範本，方便快速套用)
/// </summary>
[Table("billing_templates")]
public class BillingTemplate : IBillingProfile
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    #region 月租費用

    [Column("monthly_fee")]
    [Precision(10, 2)]
    public decimal MonthlyFee { get; set; } = 0;

    #endregion

    #region 張數計費

    [Column("page_method")]
    public PageMethod PageMethod { get; set; } = PageMethod.None;

    [Column("price_per_black")]
    [Precision(10, 4)]
    public decimal PricePerBlack { get; set; } = 0;

    [Column("price_per_color")]
    [Precision(10, 4)]
    public decimal PricePerColor { get; set; } = 0;

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

    [Column("free_black_pages")]
    public int FreeBlackPages { get; set; } = 0;

    [Column("free_color_pages")]
    public int FreeColorPages { get; set; } = 0;

    [Column("free_large_pages")]
    public int FreeLargePages { get; set; } = 0;

    #endregion

    #region 計費週期

    [Column("monthly_fee_cycle")]
    public int MonthlyFeeCycle { get; set; } = 1;

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

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<BillingTier> Tiers { get; set; } = new List<BillingTier>();

    public virtual ICollection<BillingTemplateSheetPrice> SheetPrices { get; set; } = new List<BillingTemplateSheetPrice>();

    public virtual ICollection<BillingTemplateSheetTier> SheetTiers { get; set; } = new List<BillingTemplateSheetTier>();
}

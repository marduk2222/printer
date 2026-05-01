using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace printer.Data.Entities;

/// <summary>
/// 計費群組 — 多台事務機合併計算超印費（張數共用贈送、誤印率、單價、階梯）
/// 月租仍由各台 PrinterBillingConfig 獨立。
/// 群組成員可跨 Partner，整張帳單以 BillingPartnerId（結帳客戶）為單位。
/// </summary>
[Table("printer_billing_groups")]
public class PrinterBillingGroup
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 結帳客戶 ID — 群組合併費用與成員月租都掛在此 Partner 的帳單下
    /// </summary>
    [Column("billing_partner_id")]
    public int BillingPartnerId { get; set; }

    [Column("name")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 啟用後，新增/移除成員時自動把該機台各類別贈送張數加入/扣抵群組共用池
    /// </summary>
    [Column("auto_adjust_gift_pool")]
    public bool AutoAdjustGiftPool { get; set; } = false;

    /// <summary>
    /// 張數計費週期（月數），1=每月, 3=每季, 6=半年, 12=每年
    /// </summary>
    [Column("page_fee_cycle")]
    public int PageFeeCycle { get; set; } = 1;

    [Column("page_start_date")]
    public DateOnly? PageStartDate { get; set; }

    [Column("last_page_billed_date")]
    public DateOnly? LastPageBilledDate { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("BillingPartnerId")]
    public virtual Partner? BillingPartner { get; set; }

    public virtual ICollection<Printer> Members { get; set; } = new List<Printer>();
    public virtual ICollection<BillingGroupSheetPrice> SheetPrices { get; set; } = new List<BillingGroupSheetPrice>();
    public virtual ICollection<BillingGroupSheetTier> SheetTiers { get; set; } = new List<BillingGroupSheetTier>();
}

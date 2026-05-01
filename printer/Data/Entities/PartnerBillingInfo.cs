using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 客戶帳單寄送資訊
/// </summary>
[Table("partner_billing_infos")]
public class PartnerBillingInfo
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 客戶 ID
    /// </summary>
    [Column("partner_id")]
    public int PartnerId { get; set; }

    /// <summary>
    /// 帳單 Email
    /// </summary>
    [Column("email")]
    [MaxLength(200)]
    public string? Email { get; set; }

    /// <summary>
    /// 帳單地址
    /// </summary>
    [Column("address")]
    [MaxLength(500)]
    public string? Address { get; set; }

    /// <summary>
    /// 顯示排序（由小到大；同 partner 內可拖曳調整）
    /// </summary>
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("PartnerId")]
    public virtual Partner? Partner { get; set; }
}

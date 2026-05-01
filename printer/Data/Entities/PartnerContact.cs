using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 客戶連絡人
/// </summary>
[Table("partner_contacts")]
public class PartnerContact
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
    /// 連絡人名稱
    /// </summary>
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 電話
    /// </summary>
    [Column("phone")]
    [MaxLength(50)]
    public string? Phone { get; set; }

    /// <summary>
    /// 手機
    /// </summary>
    [Column("mobile")]
    [MaxLength(50)]
    public string? Mobile { get; set; }

    /// <summary>
    /// 地址
    /// </summary>
    [Column("address")]
    [MaxLength(500)]
    public string? Address { get; set; }

    /// <summary>
    /// Email
    /// </summary>
    [Column("email")]
    [MaxLength(200)]
    public string? Email { get; set; }

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

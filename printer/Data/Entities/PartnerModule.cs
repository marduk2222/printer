using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 客戶啟用的模組 (不同客戶可啟用不同模組)
/// </summary>
[Table("partner_modules")]
public class PartnerModule
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("partner_id")]
    public int PartnerId { get; set; }

    [Column("module_id")]
    public int ModuleId { get; set; }

    /// <summary>
    /// 是否啟用
    /// </summary>
    [Column("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [Column("enabled_at")]
    public DateTime EnabledAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("PartnerId")]
    public virtual Partner? Partner { get; set; }

    [ForeignKey("ModuleId")]
    public virtual SystemModule? Module { get; set; }
}

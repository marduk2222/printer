using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 發票平台設定（系統預定義，使用者僅設定金鑰與啟用）
/// </summary>
[Table("einvoice_platforms")]
public class EinvoicePlatform
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 平台名稱
    /// </summary>
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 平台代碼 (系統定義，不可改)
    /// </summary>
    [Column("code")]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 商店代號 / Merchant ID
    /// </summary>
    [Column("merchant_id")]
    [MaxLength(100)]
    public string? MerchantId { get; set; }

    /// <summary>
    /// API Key / Hash Key
    /// </summary>
    [Column("api_key")]
    [MaxLength(500)]
    public string? ApiKey { get; set; }

    /// <summary>
    /// API Secret / Hash IV
    /// </summary>
    [Column("api_secret")]
    [MaxLength(500)]
    public string? ApiSecret { get; set; }

    /// <summary>
    /// 是否為測試模式
    /// </summary>
    [Column("is_sandbox")]
    public bool IsSandbox { get; set; } = true;

    /// <summary>
    /// 是否啟用（同一時間只能啟用一家）
    /// </summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = false;

    /// <summary>
    /// 說明 (系統預設)
    /// </summary>
    [Column("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// API 正式網址 (系統預設)
    /// </summary>
    [Column("api_url")]
    [MaxLength(500)]
    public string? ApiUrl { get; set; }

    /// <summary>
    /// API 測試網址 (系統預設)
    /// </summary>
    [Column("sandbox_url")]
    [MaxLength(500)]
    public string? SandboxUrl { get; set; }

    /// <summary>
    /// 額外參數 (JSON)
    /// </summary>
    [Column("extra_params")]
    public string? ExtraParams { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<EinvoiceFieldMapping> FieldMappings { get; set; } = new List<EinvoiceFieldMapping>();
}

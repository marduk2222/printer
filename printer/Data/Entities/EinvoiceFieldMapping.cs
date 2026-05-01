using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 發票平台欄位對應
/// </summary>
[Table("einvoice_field_mappings")]
public class EinvoiceFieldMapping
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 平台 ID
    /// </summary>
    [Column("platform_id")]
    public int PlatformId { get; set; }

    /// <summary>
    /// 系統欄位代碼 (如: invoice_number, buyer_tax_id, amount)
    /// </summary>
    [Column("field_code")]
    [MaxLength(50)]
    public string FieldCode { get; set; } = string.Empty;

    /// <summary>
    /// 平台 API 參數名稱 (如: InvNum, BuyerIdentifier, SalesAmount)
    /// </summary>
    [Column("api_param_name")]
    [MaxLength(100)]
    public string ApiParamName { get; set; } = string.Empty;

    /// <summary>
    /// 預設值 (可選，若系統欄位無值時使用)
    /// </summary>
    [Column("default_value")]
    [MaxLength(200)]
    public string? DefaultValue { get; set; }

    /// <summary>
    /// 值轉換格式 (可選，如日期格式 "yyyyMMdd")
    /// </summary>
    [Column("format")]
    [MaxLength(100)]
    public string? Format { get; set; }

    /// <summary>
    /// 是否必填
    /// </summary>
    [Column("is_required")]
    public bool IsRequired { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    [Column("sort_order")]
    public int SortOrder { get; set; }

    [ForeignKey("PlatformId")]
    public virtual EinvoicePlatform? Platform { get; set; }
}

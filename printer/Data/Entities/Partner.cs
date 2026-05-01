using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 客戶/合作夥伴
/// </summary>
[Table("partners")]
public class Partner
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 客戶代碼
    /// </summary>
    [Column("code")]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 客戶編號
    /// </summary>
    [Column("partner_number")]
    [MaxLength(50)]
    public string? PartnerNumber { get; set; }

    /// <summary>
    /// 客戶名稱
    /// </summary>
    [Column("name")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 是否為公司
    /// </summary>
    [Column("is_company")]
    public bool IsCompany { get; set; } = true;

    /// <summary>
    /// 統一編號
    /// </summary>
    [Column("vat")]
    [MaxLength(20)]
    public string? Vat { get; set; }

    /// <summary>
    /// 電話
    /// </summary>
    [Column("phone")]
    [MaxLength(50)]
    public string? Phone { get; set; }

    /// <summary>
    /// 傳真
    /// </summary>
    [Column("fax")]
    [MaxLength(50)]
    public string? Fax { get; set; }

    /// <summary>
    /// 手機
    /// </summary>
    [Column("mobile")]
    [MaxLength(50)]
    public string? Mobile { get; set; }

    /// <summary>
    /// Email
    /// </summary>
    [Column("email")]
    [MaxLength(200)]
    public string? Email { get; set; }

    /// <summary>
    /// 地址
    /// </summary>
    [Column("address")]
    [MaxLength(500)]
    public string? Address { get; set; }

    /// <summary>
    /// 聯絡人
    /// </summary>
    [Column("contact_name")]
    [MaxLength(100)]
    public string? ContactName { get; set; }

    /// <summary>
    /// 備註
    /// </summary>
    [Column("note")]
    public string? Note { get; set; }

    /// <summary>
    /// 發票統編 (買方統編；A 公司可開立 B 公司統編，故與基本資料 Vat 獨立。空白時沿用 Vat)
    /// </summary>
    [Column("invoice_tax_id")]
    [MaxLength(20)]
    public string? InvoiceTaxId { get; set; }

    /// <summary>
    /// 發票抬頭 (空白時預設使用 Name)
    /// </summary>
    [Column("invoice_title")]
    [MaxLength(200)]
    public string? InvoiceTitle { get; set; }

    /// <summary>
    /// 載具類型: none=紙本, mobile=手機條碼, citizen=自然人憑證, donate=捐贈
    /// </summary>
    [Column("invoice_carrier_type")]
    [MaxLength(20)]
    public string? InvoiceCarrierType { get; set; }

    /// <summary>
    /// 載具號碼 (手機條碼 /XXXXXXX、自然人憑證 16 碼)
    /// </summary>
    [Column("invoice_carrier_id")]
    [MaxLength(64)]
    public string? InvoiceCarrierId { get; set; }

    /// <summary>
    /// 愛心碼 (捐贈用，3-7 位數字)
    /// </summary>
    [Column("invoice_donation_code")]
    [MaxLength(10)]
    public string? InvoiceDonationCode { get; set; }

    /// <summary>
    /// 發票寄送 Email
    /// </summary>
    [Column("invoice_email")]
    [MaxLength(200)]
    public string? InvoiceEmail { get; set; }

    /// <summary>
    /// 預設課稅別: taxable=應稅, zero=零稅率, free=免稅
    /// </summary>
    [Column("invoice_default_tax_type")]
    [MaxLength(20)]
    public string? InvoiceDefaultTaxType { get; set; }

    /// <summary>
    /// 開立發票時的預設備註
    /// </summary>
    [Column("invoice_note")]
    [MaxLength(500)]
    public string? InvoiceNote { get; set; }

    /// <summary>
    /// 是否啟用
    /// </summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 狀態: 1=啟用, 0=停用
    /// </summary>
    [Column("state")]
    [MaxLength(10)]
    public string State { get; set; } = "1";

    /// <summary>
    /// 建立時間
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新時間
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<Printer> Printers { get; set; } = new List<Printer>();
    public virtual ICollection<PartnerContact> Contacts { get; set; } = new List<PartnerContact>();
    public virtual ICollection<PartnerBillingInfo> BillingInfos { get; set; } = new List<PartnerBillingInfo>();
}

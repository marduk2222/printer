using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace printer.Data.Entities;

/// <summary>
/// 電子發票
/// </summary>
[Table("einvoices")]
public class Einvoice
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 發票號碼 (如: AB-12345678)
    /// </summary>
    [Column("invoice_number")]
    [MaxLength(20)]
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// 發票日期
    /// </summary>
    [Column("invoice_date")]
    public DateOnly InvoiceDate { get; set; }

    /// <summary>
    /// 發票平台 ID
    /// </summary>
    [Column("platform_id")]
    public int? PlatformId { get; set; }

    /// <summary>
    /// 關聯帳單 ID (可選)
    /// </summary>
    [Column("billing_invoice_id")]
    public int? BillingInvoiceId { get; set; }

    /// <summary>
    /// 客戶 ID
    /// </summary>
    [Column("partner_id")]
    public int PartnerId { get; set; }

    /// <summary>
    /// 買方統一編號
    /// </summary>
    [Column("buyer_tax_id")]
    [MaxLength(20)]
    public string? BuyerTaxId { get; set; }

    /// <summary>
    /// 買方名稱
    /// </summary>
    [Column("buyer_name")]
    [MaxLength(200)]
    public string BuyerName { get; set; } = string.Empty;

    /// <summary>
    /// 買方 Email (發票寄送用)
    /// </summary>
    [Column("buyer_email")]
    [MaxLength(200)]
    public string? BuyerEmail { get; set; }

    /// <summary>
    /// 載具類型: none=紙本, mobile=手機條碼, citizen=自然人憑證, donate=捐贈
    /// </summary>
    [Column("carrier_type")]
    [MaxLength(20)]
    public string? CarrierType { get; set; }

    /// <summary>
    /// 載具號碼
    /// </summary>
    [Column("carrier_id")]
    [MaxLength(64)]
    public string? CarrierId { get; set; }

    /// <summary>
    /// 愛心碼
    /// </summary>
    [Column("donation_code")]
    [MaxLength(10)]
    public string? DonationCode { get; set; }

    /// <summary>
    /// 賣方統一編號
    /// </summary>
    [Column("seller_tax_id")]
    [MaxLength(20)]
    public string? SellerTaxId { get; set; }

    /// <summary>
    /// 賣方名稱
    /// </summary>
    [Column("seller_name")]
    [MaxLength(200)]
    public string? SellerName { get; set; }

    /// <summary>
    /// 未稅金額
    /// </summary>
    [Column("amount")]
    [Precision(12, 2)]
    public decimal Amount { get; set; }

    /// <summary>
    /// 稅額
    /// </summary>
    [Column("tax_amount")]
    [Precision(12, 2)]
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// 含稅總額
    /// </summary>
    [Column("total_amount")]
    [Precision(12, 2)]
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// 稅率 (%)
    /// </summary>
    [Column("tax_rate")]
    public int TaxRate { get; set; } = 5;

    /// <summary>
    /// 課稅類別: taxable=應稅, zero=零稅率, free=免稅
    /// </summary>
    [Column("tax_type")]
    [MaxLength(20)]
    public string TaxType { get; set; } = "taxable";

    /// <summary>
    /// 狀態: draft=草稿, issued=已開立, void=已作廢
    /// </summary>
    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "draft";

    /// <summary>
    /// 作廢原因
    /// </summary>
    [Column("void_reason")]
    [MaxLength(500)]
    public string? VoidReason { get; set; }

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

    [Column("issued_at")]
    public DateTime? IssuedAt { get; set; }

    [Column("void_at")]
    public DateTime? VoidAt { get; set; }

    // Navigation
    [ForeignKey("PlatformId")]
    public virtual EinvoicePlatform? Platform { get; set; }

    [ForeignKey("PartnerId")]
    public virtual Partner? Partner { get; set; }

    [ForeignKey("BillingInvoiceId")]
    public virtual Invoice? BillingInvoice { get; set; }

    public virtual ICollection<EinvoiceItem> Items { get; set; } = new List<EinvoiceItem>();
}

/// <summary>
/// 電子發票明細
/// </summary>
[Table("einvoice_items")]
public class EinvoiceItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("einvoice_id")]
    public int EinvoiceId { get; set; }

    /// <summary>
    /// 品名
    /// </summary>
    [Column("description")]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 數量
    /// </summary>
    [Column("quantity")]
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// 單價
    /// </summary>
    [Column("unit_price")]
    [Precision(10, 2)]
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// 小計
    /// </summary>
    [Column("subtotal")]
    [Precision(10, 2)]
    public decimal Subtotal { get; set; }

    [ForeignKey("EinvoiceId")]
    public virtual Einvoice? Einvoice { get; set; }
}

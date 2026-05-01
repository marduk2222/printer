using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace printer.Data.Entities;

/// <summary>
/// 帳單
/// </summary>
[Table("invoices")]
public class Invoice
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 帳單編號 (如: INV-202602-001)
    /// </summary>
    [Column("invoice_number")]
    [MaxLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// 客戶 ID
    /// </summary>
    [Column("partner_id")]
    public int PartnerId { get; set; }

    /// <summary>
    /// 帳單期間 (如: 20260201-20260228 或 20260101-20260630)
    /// </summary>
    [Column("billing_period")]
    [MaxLength(20)]
    public string BillingPeriod { get; set; } = string.Empty;

    /// <summary>
    /// 帳單起始日
    /// </summary>
    [Column("period_start")]
    public DateOnly PeriodStart { get; set; }

    /// <summary>
    /// 帳單結束日
    /// </summary>
    [Column("period_end")]
    public DateOnly PeriodEnd { get; set; }

    /// <summary>
    /// 總金額
    /// </summary>
    [Column("total_amount")]
    [Precision(12, 2)]
    public decimal TotalAmount { get; set; } = 0;

    /// <summary>
    /// 稅額
    /// </summary>
    [Column("tax_amount")]
    [Precision(12, 2)]
    public decimal TaxAmount { get; set; } = 0;

    /// <summary>
    /// 含稅總額
    /// </summary>
    [Column("grand_total")]
    [Precision(12, 2)]
    public decimal GrandTotal { get; set; } = 0;

    /// <summary>
    /// 狀態: draft=草稿, confirmed=已確認, paid=已付款, cancelled=已取消
    /// </summary>
    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "draft";

    /// <summary>
    /// 備註
    /// </summary>
    [Column("note")]
    public string? Note { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("confirmed_at")]
    public DateTime? ConfirmedAt { get; set; }

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    // Navigation
    [ForeignKey("PartnerId")]
    public virtual Partner? Partner { get; set; }

    public virtual ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}

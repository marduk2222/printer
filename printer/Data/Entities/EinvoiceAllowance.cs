using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace printer.Data.Entities;

/// <summary>
/// 電子發票折讓單
/// </summary>
[Table("einvoice_allowances")]
public class EinvoiceAllowance
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 折讓單號 (平台回傳)
    /// </summary>
    [Column("allowance_number")]
    [MaxLength(50)]
    public string? AllowanceNumber { get; set; }

    /// <summary>
    /// 折讓日期
    /// </summary>
    [Column("allowance_date")]
    public DateOnly AllowanceDate { get; set; }

    /// <summary>
    /// 對應原發票 ID
    /// </summary>
    [Column("einvoice_id")]
    public int EinvoiceId { get; set; }

    /// <summary>
    /// 平台 ID (繼承自原發票)
    /// </summary>
    [Column("platform_id")]
    public int? PlatformId { get; set; }

    /// <summary>
    /// 折讓未稅金額
    /// </summary>
    [Column("amount")]
    [Precision(12, 2)]
    public decimal Amount { get; set; }

    /// <summary>
    /// 折讓稅額
    /// </summary>
    [Column("tax_amount")]
    [Precision(12, 2)]
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// 折讓總額 (含稅)
    /// </summary>
    [Column("total_amount")]
    [Precision(12, 2)]
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// 折讓類型: paper=紙本, online=線上(通知開立)
    /// </summary>
    [Column("allowance_type")]
    [MaxLength(20)]
    public string AllowanceType { get; set; } = "paper";

    /// <summary>
    /// 狀態: draft=草稿, issued=已開立, void=已作廢
    /// </summary>
    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "draft";

    [Column("void_reason")]
    [MaxLength(500)]
    public string? VoidReason { get; set; }

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

    [ForeignKey("EinvoiceId")]
    public virtual Einvoice? Einvoice { get; set; }

    [ForeignKey("PlatformId")]
    public virtual EinvoicePlatform? Platform { get; set; }

    public virtual ICollection<EinvoiceAllowanceItem> Items { get; set; } = new List<EinvoiceAllowanceItem>();
}

[Table("einvoice_allowance_items")]
public class EinvoiceAllowanceItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("allowance_id")]
    public int AllowanceId { get; set; }

    [Column("description")]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Column("quantity")]
    public int Quantity { get; set; } = 1;

    [Column("unit_price")]
    [Precision(10, 2)]
    public decimal UnitPrice { get; set; }

    [Column("subtotal")]
    [Precision(10, 2)]
    public decimal Subtotal { get; set; }

    [ForeignKey("AllowanceId")]
    public virtual EinvoiceAllowance? Allowance { get; set; }
}

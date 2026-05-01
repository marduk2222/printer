using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 帳單列印設定（單例，始終只有 1 筆）
/// </summary>
[Table("invoice_print_settings")]
public class InvoicePrintSettings
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 樣板代碼：classic | modern | traditional
    /// </summary>
    [Column("template_code")]
    [MaxLength(50)]
    public string TemplateCode { get; set; } = "classic";

    // ─── 公司資訊 ───────────────────────────────────────────────

    [Column("company_name")]
    [MaxLength(200)]
    public string? CompanyName { get; set; }

    [Column("company_slogan")]
    [MaxLength(200)]
    public string? CompanySlogan { get; set; }

    [Column("company_address")]
    [MaxLength(500)]
    public string? CompanyAddress { get; set; }

    [Column("company_phone")]
    [MaxLength(100)]
    public string? CompanyPhone { get; set; }

    [Column("company_email")]
    [MaxLength(200)]
    public string? CompanyEmail { get; set; }

    [Column("company_website")]
    [MaxLength(200)]
    public string? CompanyWebsite { get; set; }

    /// <summary>
    /// 公司 Logo URL
    /// </summary>
    [Column("company_logo_url")]
    [MaxLength(500)]
    public string? CompanyLogoUrl { get; set; }

    // ─── 匯款資訊 ───────────────────────────────────────────────

    [Column("bank_name")]
    [MaxLength(200)]
    public string? BankName { get; set; }

    [Column("bank_branch")]
    [MaxLength(200)]
    public string? BankBranch { get; set; }

    [Column("bank_account")]
    [MaxLength(200)]
    public string? BankAccount { get; set; }

    /// <summary>
    /// 付款條件，例如「立即付款」
    /// </summary>
    [Column("payment_terms")]
    [MaxLength(500)]
    public string? PaymentTerms { get; set; }

    [Column("footer_note")]
    [MaxLength(1000)]
    public string? FooterNote { get; set; }

    /// <summary>
    /// 主色調（十六進位色碼，例如 #1e6b45）
    /// </summary>
    [Column("primary_color")]
    [MaxLength(20)]
    public string? PrimaryColor { get; set; }
}

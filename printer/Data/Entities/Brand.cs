using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 事務機品牌
/// </summary>
[Table("brands")]
public class Brand
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 品牌名稱
    /// </summary>
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 說明
    /// </summary>
    [Column("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    /// <summary>
    /// 圖片路徑
    /// </summary>
    [Column("image_path")]
    [MaxLength(500)]
    public string? ImagePath { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<PrinterModel> Models { get; set; } = new List<PrinterModel>();
}

/// <summary>
/// 事務機型號
/// </summary>
[Table("printer_models")]
public class PrinterModel
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 型號代碼
    /// </summary>
    [Column("code")]
    [MaxLength(50)]
    public string? Code { get; set; }

    /// <summary>
    /// 型號名稱
    /// </summary>
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 品牌 ID
    /// </summary>
    [Column("brand_id")]
    public int BrandId { get; set; }

    /// <summary>
    /// 功能特性
    /// </summary>
    [Column("feature")]
    [MaxLength(500)]
    public string? Feature { get; set; }

    /// <summary>
    /// 說明
    /// </summary>
    [Column("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// 是否已驗證
    /// </summary>
    [Column("verify")]
    public bool Verify { get; set; }

    /// <summary>
    /// 狀態: 1=啟用, 0=停用
    /// </summary>
    [Column("state")]
    [MaxLength(10)]
    public string State { get; set; } = "1";

    /// <summary>
    /// 建立時間
    /// </summary>
    /// <summary>
    /// 圖片路徑
    /// </summary>
    [Column("image_path")]
    [MaxLength(500)]
    public string? ImagePath { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("BrandId")]
    public virtual Brand? Brand { get; set; }

    public virtual ICollection<Printer> Printers { get; set; } = new List<Printer>();

    public virtual ICollection<PrinterModelSheetType> PrinterModelSheetTypes { get; set; } = new List<PrinterModelSheetType>();
}

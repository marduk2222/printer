using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 派工單附圖
/// </summary>
[Table("work_order_images")]
public class WorkOrderImage
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("work_order_id")]
    public int WorkOrderId { get; set; }

    /// <summary>
    /// 原始檔案名稱
    /// </summary>
    [Column("file_name")]
    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 儲存路徑（相對於 wwwroot）
    /// </summary>
    [Column("file_path")]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 圖片說明
    /// </summary>
    [Column("caption")]
    [MaxLength(200)]
    public string? Caption { get; set; }

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("WorkOrderId")]
    public virtual WorkOrder? WorkOrder { get; set; }
}

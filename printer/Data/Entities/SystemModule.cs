using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 系統模組定義
/// </summary>
[Table("system_modules")]
public class SystemModule
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 模組代碼 (唯一識別, 如: billing, report, alert)
    /// </summary>
    [Column("code")]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 模組名稱
    /// </summary>
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 模組說明
    /// </summary>
    [Column("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// 是否全域啟用
    /// </summary>
    [Column("is_enabled")]
    public bool IsEnabled { get; set; } = false;

    /// <summary>
    /// 排序順序
    /// </summary>
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// 模組圖示 (Bootstrap Icons 類別)
    /// </summary>
    [Column("icon")]
    [MaxLength(50)]
    public string? Icon { get; set; }

    /// <summary>
    /// 導覽列路徑 (如: /Invoice)
    /// </summary>
    [Column("menu_path")]
    [MaxLength(100)]
    public string? MenuPath { get; set; }

    /// <summary>
    /// 導覽列控制器名稱
    /// </summary>
    [Column("menu_controller")]
    [MaxLength(50)]
    public string? MenuController { get; set; }

    /// <summary>
    /// 導覽列動作名稱
    /// </summary>
    [Column("menu_action")]
    [MaxLength(50)]
    public string? MenuAction { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

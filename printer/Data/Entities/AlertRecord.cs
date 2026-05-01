using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 告警回傳紀錄
/// </summary>
[Table("alert_records")]
public class AlertRecord
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 事務機 ID
    /// </summary>
    [Column("printer_id")]
    public int PrinterId { get; set; }

    /// <summary>
    /// 告警代碼
    /// </summary>
    [Column("code")]
    public int Code { get; set; }

    /// <summary>
    /// 告警訊息
    /// </summary>
    [Column("message")]
    [MaxLength(500)]
    public string? Message { get; set; }

    /// <summary>
    /// 狀態: warn=警告, error=錯誤, resolved=已解決
    /// </summary>
    [Column("state")]
    [MaxLength(20)]
    public string State { get; set; } = "warn";

    /// <summary>
    /// 建立時間
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 解決時間
    /// </summary>
    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    // Navigation properties
    [ForeignKey("PrinterId")]
    public virtual Printer? Printer { get; set; }
}

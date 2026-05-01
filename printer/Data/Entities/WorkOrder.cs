using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 派工單
/// </summary>
[Table("work_orders")]
public class WorkOrder
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 派工單號，例如：WO-202604-001
    /// </summary>
    [Column("order_number")]
    [MaxLength(30)]
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// 客戶 ID
    /// </summary>
    [Column("partner_id")]
    public int? PartnerId { get; set; }

    /// <summary>
    /// 標題
    /// </summary>
    [Column("title")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 問題描述
    /// </summary>
    [Column("description")]
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// 狀態：open=待處理, in_progress=處理中, completed=已完成, cancelled=已取消
    /// </summary>
    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "open";

    /// <summary>
    /// 建立者 ID
    /// </summary>
    [Column("created_by_id")]
    public int? CreatedById { get; set; }

    /// <summary>
    /// 預計到場時間
    /// </summary>
    [Column("scheduled_at")]
    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// 預計處理天數（自建立日起算）；超過未完成視為逾期
    /// </summary>
    [Column("expected_days")]
    public int? ExpectedDays { get; set; }

    /// <summary>
    /// 完成時間
    /// </summary>
    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 處理備註
    /// </summary>
    [Column("note")]
    [MaxLength(2000)]
    public string? Note { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("PartnerId")]
    public virtual Partner? Partner { get; set; }

    [ForeignKey("CreatedById")]
    public virtual AppUser? CreatedBy { get; set; }

    /// <summary>
    /// 指派事務機（多選）
    /// </summary>
    public virtual ICollection<WorkOrderPrinter> WorkOrderPrinters { get; set; } = new List<WorkOrderPrinter>();

    /// <summary>
    /// 指派人員（多選）
    /// </summary>
    public virtual ICollection<WorkOrderAssignee> WorkOrderAssignees { get; set; } = new List<WorkOrderAssignee>();

    public virtual ICollection<WorkOrderImage> Images { get; set; } = new List<WorkOrderImage>();
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 派工單 ↔ 指派人員（多對多）
/// </summary>
[Table("work_order_assignees")]
public class WorkOrderAssignee
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("work_order_id")]
    public int WorkOrderId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [ForeignKey("WorkOrderId")]
    public virtual WorkOrder? WorkOrder { get; set; }

    [ForeignKey("UserId")]
    public virtual AppUser? User { get; set; }
}

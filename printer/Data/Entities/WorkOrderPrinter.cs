using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 派工單 ↔ 事務機（多對多）
/// </summary>
[Table("work_order_printers")]
public class WorkOrderPrinter
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("work_order_id")]
    public int WorkOrderId { get; set; }

    [Column("printer_id")]
    public int PrinterId { get; set; }

    [ForeignKey("WorkOrderId")]
    public virtual WorkOrder? WorkOrder { get; set; }

    [ForeignKey("PrinterId")]
    public virtual Printer? Printer { get; set; }
}

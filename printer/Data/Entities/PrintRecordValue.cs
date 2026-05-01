using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 抄表記錄中每個張數類型的數值（多個 driver_key 已加總）
/// </summary>
[Table("print_record_values")]
public class PrintRecordValue
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("record_id")]
    public int RecordId { get; set; }

    [Column("sheet_type_id")]
    public int SheetTypeId { get; set; }

    /// <summary>
    /// 該類型的張數（已加總同類型的所有 driver_key）
    /// </summary>
    [Column("value")]
    public int Value { get; set; } = 0;

    /// <summary>
    /// 顯示排序（由小到大，per-record 內可拖曳調整）
    /// </summary>
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    // Navigation
    [ForeignKey("RecordId")]
    public virtual PrintRecord? Record { get; set; }

    [ForeignKey("SheetTypeId")]
    public virtual SheetType? SheetType { get; set; }
}

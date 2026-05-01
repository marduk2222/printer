using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 機型配置的張數類型（多對多關聯表）
/// </summary>
[Table("printer_model_sheet_types")]
public class PrinterModelSheetType
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("printer_model_id")]
    public int PrinterModelId { get; set; }

    [Column("sheet_type_id")]
    public int SheetTypeId { get; set; }

    /// <summary>
    /// 此機型中該張數類型的顯示排序
    /// </summary>
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    // Navigation
    [ForeignKey("PrinterModelId")]
    public virtual PrinterModel? PrinterModel { get; set; }

    [ForeignKey("SheetTypeId")]
    public virtual SheetType? SheetType { get; set; }
}

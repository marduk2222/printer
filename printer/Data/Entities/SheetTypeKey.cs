using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 張數類型對應的驅動參數（多個 driver_key 累加到同一類型）
/// </summary>
[Table("sheet_type_keys")]
public class SheetTypeKey
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("sheet_type_id")]
    public int SheetTypeId { get; set; }

    /// <summary>
    /// 驅動回傳的參數名稱，例如：print_black、print_color_single
    /// </summary>
    [Column("driver_key")]
    [MaxLength(100)]
    public string DriverKey { get; set; } = string.Empty;

    // Navigation
    [ForeignKey("SheetTypeId")]
    public virtual SheetType? SheetType { get; set; }
}

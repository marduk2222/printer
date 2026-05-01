using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 事務機維護人員 (多對多)
/// </summary>
[Table("printer_maintainers")]
public class PrinterMaintainer
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("printer_id")]
    public int PrinterId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    /// <summary>
    /// 角色: primary=主要維護, support=支援
    /// </summary>
    [Column("role")]
    [MaxLength(20)]
    public string Role { get; set; } = "support";

    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("PrinterId")]
    public virtual Printer? Printer { get; set; }

    [ForeignKey("UserId")]
    public virtual AppUser? User { get; set; }
}

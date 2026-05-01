using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 角色權限設定
/// </summary>
[Table("role_permissions")]
public class RolePermission
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 角色: employee, supervisor, admin
    /// </summary>
    [Column("role")]
    [MaxLength(20)]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// 功能代碼 (如: home, partner, printer, print_record, billing, system)
    /// </summary>
    [Column("feature_code")]
    [MaxLength(50)]
    public string FeatureCode { get; set; } = string.Empty;

    /// <summary>
    /// 是否允許
    /// </summary>
    [Column("is_allowed")]
    public bool IsAllowed { get; set; } = true;
}

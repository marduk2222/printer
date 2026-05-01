using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace printer.Data.Entities;

/// <summary>
/// 系統使用者
/// </summary>
[Table("app_users")]
public class AppUser
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 帳號
    /// </summary>
    [Column("username")]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密碼 (雜湊)
    /// </summary>
    [Column("password_hash")]
    [MaxLength(200)]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 姓名
    /// </summary>
    [Column("display_name")]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Email
    /// </summary>
    [Column("email")]
    [MaxLength(200)]
    public string? Email { get; set; }

    /// <summary>
    /// 電話
    /// </summary>
    [Column("phone")]
    [MaxLength(50)]
    public string? Phone { get; set; }

    /// <summary>
    /// 角色: employee=員工, supervisor=主管, admin=系統管理員
    /// </summary>
    [Column("role")]
    [MaxLength(20)]
    public string Role { get; set; } = "employee";

    /// <summary>
    /// 是否啟用
    /// </summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 備註
    /// </summary>
    [Column("note")]
    [MaxLength(500)]
    public string? Note { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;
}

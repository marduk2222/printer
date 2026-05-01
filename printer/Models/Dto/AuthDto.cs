namespace printer.Models.Dto;

/// <summary>
/// 驗證請求
/// </summary>
public class AuthRequest
{
    public string Account { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// 驗證回應
/// </summary>
public class AuthResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
}

/// <summary>
/// 公司查詢請求
/// </summary>
public class CompanyRequest
{
    public string? Keyword { get; set; }
}

/// <summary>
/// 公司資料
/// </summary>
public class CompanyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// 安裝記錄請求
/// </summary>
public class InstallRequest
{
    public string Code { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string? Date { get; set; }
    public string State { get; set; } = "0";
}

/// <summary>
/// 安裝記錄回應
/// </summary>
public class InstallResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
}

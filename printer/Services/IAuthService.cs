using printer.Models.Dto;

namespace printer.Services;

/// <summary>
/// 認證服務介面
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 驗證員工帳密
    /// </summary>
    Task<(AuthResponse? response, string? error)> VerifyStaffAsync(AuthRequest request);

    /// <summary>
    /// 取得公司列表
    /// </summary>
    Task<List<CompanyDto>> GetCompaniesAsync(CompanyRequest request);
}

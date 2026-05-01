using printer.Models.Dto;

namespace printer.Services;

/// <summary>
/// 版本服務介面
/// </summary>
public interface IVersionService
{
    /// <summary>
    /// 取得客戶端版本資訊
    /// </summary>
    Task<ClientVersionResponse> GetClientVersionAsync();

    /// <summary>
    /// 取得服務版本資訊
    /// </summary>
    Task<VersionCheckResponse> GetServiceVersionAsync(string clientVersion);

    /// <summary>
    /// 檢查更新
    /// </summary>
    Task<VersionCheckResponse> CheckUpdateAsync(string clientVersion);

}

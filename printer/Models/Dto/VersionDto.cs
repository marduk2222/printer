namespace printer.Models.Dto;

/// <summary>
/// 客戶端版本資訊
/// </summary>
public class ClientVersionResponse
{
    public string Version { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
}

/// <summary>
/// 服務版本請求
/// </summary>
public class ServiceVersionRequest
{
    public string Version { get; set; } = "0.0.0";
}

/// <summary>
/// 版本檢查回應
/// </summary>
public class VersionCheckResponse
{
    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public bool NeedUpdate { get; set; }
    public bool ForceUpdate { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public string Changelog { get; set; } = string.Empty;
}

/// <summary>
/// 版本更新檢查請求
/// </summary>
public class VersionCheckRequest
{
    public string Version { get; set; } = "0.0.0";
}


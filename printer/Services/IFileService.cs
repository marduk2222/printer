namespace printer.Services;

/// <summary>
/// 檔案服務介面
/// </summary>
public interface IFileService
{
    /// <summary>
    /// 取得下載檔案
    /// </summary>
    /// <returns>檔案內容、MIME 類型、是否為重導向 URL</returns>
    Task<(byte[]? content, string? mimeType, string? redirectUrl, string? error)> GetFileAsync(string filename);
}

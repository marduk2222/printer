namespace printer.Services.Impl;

/// <summary>
/// 檔案服務實作
/// </summary>
public class FileService : IFileService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileService> _logger;

    public FileService(IWebHostEnvironment environment, ILogger<FileService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<(byte[]? content, string? mimeType, string? redirectUrl, string? error)> GetFileAsync(string filename)
    {
        // 嘗試從 wwwroot/updates 目錄讀取檔案
        var filePath = Path.Combine(_environment.WebRootPath, "updates", filename);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("File not found: {Filename}", filename);
            return (null, null, null, $"File not found: {filename}");
        }

        var content = await File.ReadAllBytesAsync(filePath);
        var mimeType = GetMimeType(filename);

        _logger.LogInformation("Download file: {Filename}, Size: {Size} bytes", filename, content.Length);

        return (content, mimeType, null, null);
    }

    private static string GetMimeType(string filename)
    {
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        return extension switch
        {
            ".zip" => "application/zip",
            ".exe" => "application/octet-stream",
            ".msi" => "application/x-msi",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".xml" => "application/xml",
            _ => "application/octet-stream"
        };
    }
}

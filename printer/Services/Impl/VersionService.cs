using printer.Models.Dto;

namespace printer.Services.Impl;

/// <summary>
/// 版本服務實作
/// </summary>
public class VersionService : IVersionService
{
    private readonly IConfiguration _configuration;

    public VersionService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<ClientVersionResponse> GetClientVersionAsync()
    {
        return Task.FromResult(new ClientVersionResponse
        {
            Version = _configuration["Printer:Client:Version"] ?? "1.0.0",
            DownloadUrl = _configuration["Printer:Client:DownloadUrl"] ?? ""
        });
    }

    public Task<VersionCheckResponse> GetServiceVersionAsync(string clientVersion)
    {
        var latestVersion = _configuration["Printer:Service:Version"] ?? "1.0.0";
        var downloadUrl = _configuration["Printer:Service:DownloadUrl"] ?? "";
        var changelog = _configuration["Printer:Service:Changelog"] ?? "";
        var forceUpdate = _configuration.GetValue<bool>("Printer:Service:ForceUpdate");

        var needUpdate = CompareVersion(clientVersion, latestVersion);

        return Task.FromResult(new VersionCheckResponse
        {
            CurrentVersion = clientVersion,
            LatestVersion = latestVersion,
            NeedUpdate = needUpdate,
            ForceUpdate = forceUpdate && needUpdate,
            DownloadUrl = needUpdate ? downloadUrl : "",
            Changelog = needUpdate ? changelog : ""
        });
    }

    public Task<VersionCheckResponse> CheckUpdateAsync(string clientVersion)
    {
        var latestVersion = _configuration["Printer:Client:Version"] ?? "1.0.0";
        var downloadUrl = _configuration["Printer:Client:DownloadUrl"] ?? "";
        var changelog = _configuration["Printer:Client:Changelog"] ?? "";
        var forceUpdate = _configuration.GetValue<bool>("Printer:Client:ForceUpdate");

        var needUpdate = CompareVersion(clientVersion, latestVersion);

        return Task.FromResult(new VersionCheckResponse
        {
            CurrentVersion = clientVersion,
            LatestVersion = latestVersion,
            NeedUpdate = needUpdate,
            ForceUpdate = forceUpdate && needUpdate,
            DownloadUrl = needUpdate ? downloadUrl : "",
            Changelog = needUpdate ? changelog : ""
        });
    }

    /// <summary>
    /// 比較版本號，回傳 v1 < v2
    /// </summary>
    private static bool CompareVersion(string v1, string v2)
    {
        try
        {
            var parts1 = v1.Split('.').Select(int.Parse).ToArray();
            var parts2 = v2.Split('.').Select(int.Parse).ToArray();

            for (int i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
            {
                var p1 = i < parts1.Length ? parts1[i] : 0;
                var p2 = i < parts2.Length ? parts2[i] : 0;

                if (p1 < p2) return true;
                if (p1 > p2) return false;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}

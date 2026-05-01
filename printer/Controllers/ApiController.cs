using Microsoft.AspNetCore.Mvc;
using printer.Models;
using printer.Models.Dto;
using printer.Services;

namespace printer.Controllers;

/// <summary>
/// 印表機雲端抄表 API 控制器
/// </summary>
[ApiController]
[Route("api")]
public class ApiController : ControllerBase
{
    private readonly IPrinterService _printerService;
    private readonly IAuthService _authService;
    private readonly IVersionService _versionService;
    private readonly IBrandService _brandService;
    private readonly IFileService _fileService;
    private readonly ILogger<ApiController> _logger;

    public ApiController(
        IPrinterService printerService,
        IAuthService authService,
        IVersionService versionService,
        IBrandService brandService,
        IFileService fileService,
        ILogger<ApiController> logger)
    {
        _printerService = printerService;
        _authService = authService;
        _versionService = versionService;
        _brandService = brandService;
        _fileService = fileService;
        _logger = logger;
    }

    #region Basic Endpoints

    /// <summary>
    /// 健康檢查
    /// </summary>
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        return Ok(ApiResponse.Success("pong", new { db = "printer" }));
    }

    /// <summary>
    /// 取得印表機列表
    /// </summary>
    [HttpPost("printer")]
    public async Task<IActionResult> GetPrinters([FromBody] PrinterListRequest request)
    {
        var result = await _printerService.GetPrintersAsync(request);
        return Ok(ApiResponse.Success($"found {result.Count} printer(s)", result));
    }

    /// <summary>
    /// 取得定期同步印表機列表
    /// </summary>
    [HttpPost("period")]
    public async Task<IActionResult> GetPeriodPrinters([FromBody] PeriodRequest request)
    {
        var result = await _printerService.GetPeriodPrintersAsync(request);
        return Ok(ApiResponse.Success($"found {result.Count} printer(s)", result));
    }

    /// <summary>
    /// 更新耗材資訊
    /// </summary>
    [HttpPost("supplies")]
    public async Task<IActionResult> UpdateSupplies([FromBody] SuppliesUpdateRequest request)
    {
        if (string.IsNullOrEmpty(request.Code))
            return Ok(ApiResponse.Error("missing code"));

        var result = await _printerService.UpdateSuppliesAsync(request);
        if (result == null)
            return Ok(ApiResponse.Error("printer not found"));

        return Ok(ApiResponse.Success("supplies updated", result));
    }

    /// <summary>
    /// 更新警報
    /// </summary>
    [HttpPost("alerts")]
    public async Task<IActionResult> UpdateAlerts([FromBody] AlertsUpdateRequest request)
    {
        if (string.IsNullOrEmpty(request.Code))
            return Ok(ApiResponse.Error("missing code"));

        var result = await _printerService.UpdateAlertsAsync(request);
        if (result == null)
            return Ok(ApiResponse.Error("printer not found"));

        return Ok(ApiResponse.Success("alerts updated", result));
    }

    /// <summary>
    /// 寫入抄表記錄
    /// </summary>
    [HttpPost("record")]
    public async Task<IActionResult> WriteRecord([FromBody] RecordRequest request)
    {
        if (string.IsNullOrEmpty(request.Code))
            return Ok(ApiResponse.Error("missing code"));
        if (string.IsNullOrEmpty(request.Date))
            return Ok(ApiResponse.Error("missing date"));
        if (request.BlackPrint is null && request.ColorPrint is null && request.LargePrint is null)
            return Ok(ApiResponse.Error("no print count provided"));

        var (result, error) = await _printerService.WriteRecordAsync(request);
        if (error != null)
            return Ok(ApiResponse.Error(error));

        return Ok(ApiResponse.Success(result!.RecordId > 0 ? "meter updated" : "meter created", result));
    }

    #endregion

    #region Client API Endpoints

    /// <summary>
    /// 驗證員工帳密
    /// </summary>
    [HttpPost("auth")]
    public async Task<IActionResult> VerifyStaff([FromBody] AuthRequest request)
    {
        if (string.IsNullOrEmpty(request.Account) || string.IsNullOrEmpty(request.Password))
            return Ok(ApiResponse.Error("missing account or password"));

        var (result, error) = await _authService.VerifyStaffAsync(request);
        if (error != null)
            return Ok(ApiResponse.Error(error));

        return Ok(ApiResponse.Success("verified", result));
    }

    /// <summary>
    /// 取得公司列表
    /// </summary>
    [HttpPost("company")]
    public async Task<IActionResult> GetCompanies([FromBody] CompanyRequest request)
    {
        var result = await _authService.GetCompaniesAsync(request);
        return Ok(ApiResponse.Success($"found {result.Count} company(s)", result));
    }

    /// <summary>
    /// 更新設備資訊
    /// </summary>
    [HttpPost("device")]
    public async Task<IActionResult> UpdateDevice([FromBody] DeviceUpdateRequest request)
    {
        if (string.IsNullOrEmpty(request.Code))
            return Ok(ApiResponse.Error("missing code"));

        var result = await _printerService.UpdateDeviceAsync(request);
        if (result == null)
            return Ok(ApiResponse.Error("printer not found"));

        return Ok(ApiResponse.Success("mfp updated", result));
    }

    /// <summary>
    /// 建立安裝記錄
    /// </summary>
    [HttpPost("install")]
    public async Task<IActionResult> CreateInstall([FromBody] InstallRequest request)
    {
        if (string.IsNullOrEmpty(request.Code))
            return Ok(ApiResponse.Error("missing code"));

        var (result, error) = await _printerService.CreateInstallAsync(request);
        if (error != null)
            return Ok(ApiResponse.Error(error));

        return Ok(ApiResponse.Success("install recorded", result));
    }

    #endregion

    #region Auto Update API Endpoints

    /// <summary>
    /// 取得客戶端版本資訊
    /// </summary>
    [HttpGet("client/version")]
    public async Task<IActionResult> GetClientVersion()
    {
        var result = await _versionService.GetClientVersionAsync();
        return Ok(ApiResponse.Success("ok", result));
    }

    /// <summary>
    /// 取得服務版本資訊
    /// </summary>
    [HttpGet("service/version")]
    public async Task<IActionResult> GetServiceVersion([FromQuery] string version = "0.0.0")
    {
        var result = await _versionService.GetServiceVersionAsync(version);
        return Ok(ApiResponse.Success("ok", result));
    }

    /// <summary>
    /// 檢查更新
    /// </summary>
    [HttpPost("version")]
    public async Task<IActionResult> CheckUpdate([FromBody] VersionCheckRequest request)
    {
        var result = await _versionService.CheckUpdateAsync(request.Version);
        return Ok(ApiResponse.Success("ok", result));
    }

    #endregion

    #region Brand & Model API Endpoints

    /// <summary>
    /// 取得所有品牌
    /// </summary>
    [HttpPost("brand")]
    public async Task<IActionResult> GetBrands()
    {
        var result = await _brandService.GetBrandsAsync();
        return Ok(ApiResponse.Success($"found {result.Count} brand(s)", result));
    }

    /// <summary>
    /// 取得型號列表
    /// </summary>
    [HttpPost("model")]
    public async Task<IActionResult> GetModels([FromBody] ModelRequest request)
    {
        var result = await _brandService.GetModelsAsync(request);
        return Ok(ApiResponse.Success($"found {result.Count} model(s)", result));
    }

    /// <summary>
    /// 取得品牌與其型號
    /// </summary>
    [HttpPost("brand/model")]
    public async Task<IActionResult> GetBrandsWithModels()
    {
        var result = await _brandService.GetBrandsWithModelsAsync();
        return Ok(ApiResponse.Success($"found {result.Count} brand(s)", result));
    }

    #endregion

    #region File Download Endpoint

    /// <summary>
    /// 下載檔案
    /// </summary>
    [HttpGet("/download/{filename}")]
    public async Task<IActionResult> DownloadFile(string filename)
    {
        if (string.IsNullOrEmpty(filename))
            return BadRequest("Filename required");

        var (content, mimeType, redirectUrl, error) = await _fileService.GetFileAsync(filename);

        if (redirectUrl != null)
            return Redirect(redirectUrl);

        if (error != null)
            return NotFound(error);

        return File(content!, mimeType ?? "application/octet-stream", filename);
    }

    #endregion
}

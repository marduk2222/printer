using printer.Data.Entities;

namespace printer.Services.Invoicing;

/// <summary>
/// 電子發票平台串接介面：開立 / 作廢 / 折讓 / 作廢折讓
/// </summary>
public interface IEinvoiceProvider
{
    /// <summary>平台代碼 (對應 EinvoicePlatform.Code)</summary>
    string Code { get; }

    /// <summary>開立發票</summary>
    Task<EinvoiceProviderResult> IssueAsync(Einvoice einvoice, EinvoicePlatform platform, CancellationToken ct = default);

    /// <summary>作廢發票</summary>
    Task<EinvoiceProviderResult> InvalidAsync(Einvoice einvoice, string reason, EinvoicePlatform platform, CancellationToken ct = default);

    /// <summary>開立折讓</summary>
    Task<EinvoiceProviderResult> AllowanceAsync(EinvoiceAllowance allowance, Einvoice einvoice, EinvoicePlatform platform, CancellationToken ct = default);

    /// <summary>作廢折讓</summary>
    Task<EinvoiceProviderResult> AllowanceInvalidAsync(EinvoiceAllowance allowance, string reason, EinvoicePlatform platform, CancellationToken ct = default);
}

/// <summary>
/// Provider 統一回傳格式
/// </summary>
public class EinvoiceProviderResult
{
    public bool Success { get; init; }

    /// <summary>平台回傳的單號 (發票號碼 / 折讓單號)</summary>
    public string? Number { get; init; }

    /// <summary>平台原始 status code</summary>
    public string? Code { get; init; }

    /// <summary>訊息 (錯誤或成功描述)</summary>
    public string? Message { get; init; }

    /// <summary>原始回應 JSON (僅供記錄用)</summary>
    public string? RawResponse { get; init; }

    /// <summary>
    /// 平台實際指派的開立日期（若有回傳）；ECPay B2C/B2B 的 Issue 回應會帶 InvoiceDate。
    /// Controller 收到後需寫回 Einvoice.InvoiceDate，否則後續折讓/作廢用本地日期查找會失敗。
    /// </summary>
    public DateOnly? IssueDate { get; init; }

    /// <summary>
    /// Provider 端用來對應原發票的識別碼（如 ezPay MerchantOrderNo、Gateweb relateNumber）。
    /// Controller 寫入 Einvoice.ProviderRelateNumber，折讓/作廢時 Provider 取回送出。
    /// </summary>
    public string? ProviderRelateNumber { get; init; }

    public static EinvoiceProviderResult Ok(string? number = null, string? code = null, string? message = null, string? raw = null, DateOnly? issueDate = null, string? providerRelateNumber = null) =>
        new() { Success = true, Number = number, Code = code, Message = message, RawResponse = raw, IssueDate = issueDate, ProviderRelateNumber = providerRelateNumber };

    public static EinvoiceProviderResult Fail(string message, string? code = null, string? raw = null) =>
        new() { Success = false, Message = message, Code = code, RawResponse = raw };
}

/// <summary>
/// 依啟用中的平台取得對應 provider
/// </summary>
public interface IEinvoiceProviderFactory
{
    /// <summary>取得指定平台 code 的 provider；找不到則 throw</summary>
    IEinvoiceProvider Get(string platformCode);

    /// <summary>取得目前啟用中的 provider 與平台設定；若無啟用則回傳 null</summary>
    Task<(IEinvoiceProvider Provider, EinvoicePlatform Platform)?> GetActiveAsync(CancellationToken ct = default);
}

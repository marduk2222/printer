using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using printer.Data.Entities;

namespace printer.Services.Invoicing.Providers;

/// <summary>
/// 關網 Gateweb 電子發票串接。
/// 文件：invoice/gateweb/Gateweb 境內測試 API 規格...html
/// 流程：username/password → /api/authenticate → id_token → Bearer 呼叫 C0403/C0503/D0403/D0503
/// 平台欄位對應：MerchantId=companyKey, ApiKey=username, ApiSecret=password
/// </summary>
public class GatewebProvider : IEinvoiceProvider
{
    public string Code => "gateweb";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GatewebProvider> _logger;

    // 簡易 token cache：以 platform.Id 為 key；JWT 預設有效期，這裡保守 50 分鐘重取
    private static readonly ConcurrentDictionary<int, (string Token, DateTime ExpireAt)> TokenCache = new();
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(50);

    public GatewebProvider(IHttpClientFactory httpClientFactory, ILogger<GatewebProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private string BaseUrl(EinvoicePlatform p) =>
        (p.IsSandbox ? "https://sstest.gwis.com.tw" : "https://ss.gwis.com.tw").TrimEnd('/');

    public async Task<EinvoiceProviderResult> IssueAsync(Einvoice e, EinvoicePlatform p, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(p.MerchantId) || string.IsNullOrEmpty(p.ApiKey) || string.IsNullOrEmpty(p.ApiSecret))
            return EinvoiceProviderResult.Fail("Gateweb 設定不完整：需 companyKey / username / password");

        var details = e.Items.Select((it, idx) => new Dictionary<string, object?>
        {
            ["description"] = it.Description,
            ["quantity"] = it.Quantity,
            ["unit"] = "個",
            ["unitPrice"] = (int)Math.Round(it.UnitPrice),
            ["amount"] = (int)Math.Round(it.Subtotal),
            ["sequenceNumber"] = idx.ToString("D3"),
            ["remark"] = (string?)null,
            ["relateNumber"] = (string?)null
        }).ToArray();

        // Gateweb 規則：B2C(無買方統編)+應稅時，稅額必為 0，銷售額帶含稅總額
        var noBuyer = string.IsNullOrEmpty(e.BuyerTaxId);
        var taxType = MapTaxType(e.TaxType);
        var taxAmount = (taxType == "1" && noBuyer) ? 0 : (int)Math.Round(e.TaxAmount);
        var salesAmount = (taxType == "1" && noBuyer) ? (int)Math.Round(e.TotalAmount) : (int)Math.Round(e.Amount);

        // 賣方資訊優先取 ExtraParams（平台設定）；缺則 fallback 至 einvoice 自身欄位
        var sellerDepartment = TryReadExtra(p.ExtraParams, "sellerDepartment");
        var sellerIdentifier = TryReadExtra(p.ExtraParams, "sellerIdentifier") ?? e.SellerTaxId ?? string.Empty;
        var sellerName = TryReadExtra(p.ExtraParams, "sellerName") ?? e.SellerName ?? string.Empty;
        if (string.IsNullOrEmpty(sellerIdentifier) || string.IsNullOrEmpty(sellerName))
            return EinvoiceProviderResult.Fail("Gateweb 開立需要 sellerIdentifier/sellerName，請至平台設定填寫");

        var relateNumber = $"INV{e.Id:D6}{DateTime.UtcNow:HHmmss}";
        var body = new Dictionary<string, object?>
        {
            ["relateNumber"] = relateNumber,
            ["invoiceDate"] = e.InvoiceDate.ToString("yyyyMMdd"),
            ["invoiceTime"] = "23:59:59",
            ["sellerDepartment"] = sellerDepartment,
            ["sellerIdentifier"] = sellerIdentifier,
            ["sellerName"] = sellerName,
            ["buyerIdentifier"] = noBuyer ? "0000000000" : e.BuyerTaxId,
            ["buyerName"] = noBuyer ? "0000000000" : e.BuyerName,
            ["buyerEmailAddress"] = e.BuyerEmail ?? string.Empty,
            ["mainRemark"] = e.Note ?? string.Empty,
            ["invoiceType"] = "07",
            ["donateMark"] = e.CarrierType == "donate" ? "1" : "0",
            ["npoban"] = e.CarrierType == "donate" ? e.DonationCode ?? string.Empty : string.Empty,
            ["printMark"] = string.IsNullOrEmpty(e.CarrierType) || e.CarrierType == "none" ? "Y" : "N",
            ["carrierType"] = MapCarrierType(e.CarrierType),
            ["carrierId1"] = e.CarrierId ?? string.Empty,
            ["carrierId2"] = e.CarrierId ?? string.Empty,
            ["detail"] = details,
            ["salesAmount"] = salesAmount,
            ["freeTaxSalesAmount"] = e.TaxType == "free" ? (int)Math.Round(e.Amount) : 0,
            ["ZeroTaxSalesAmount"] = e.TaxType == "zero" ? (int)Math.Round(e.Amount) : 0,
            ["taxType"] = taxType,
            ["taxRate"] = e.TaxType == "taxable" ? 0.05 : 0.0,
            ["taxAmount"] = taxAmount,
            ["totalAmount"] = (int)Math.Round(e.TotalAmount)
        };

        var url = $"{BaseUrl(p)}/api/v1/simplified/C0403?domestic=true&companyKey={Uri.EscapeDataString(p.MerchantId)}";
        var json = await PostWithAuthAsync(url, body, p, ct);
        var result = ParseResult(json, "invoiceNumber");

        // 開立成功但 typeCode/Success 回應沒帶 invoiceNumber，需要異步查詢
        if (result.Success && string.IsNullOrEmpty(result.Number))
        {
            var queried = await QueryInvoiceNumberAsync(sellerIdentifier, relateNumber, p, ct);
            if (!string.IsNullOrEmpty(queried))
                return EinvoiceProviderResult.Ok(queried, result.Code, result.Message, result.RawResponse, providerRelateNumber: relateNumber);
        }
        // 成功則回傳含 ProviderRelateNumber，後續折讓/作廢用得到
        return result.Success
            ? EinvoiceProviderResult.Ok(result.Number, result.Code, result.Message, result.RawResponse, result.IssueDate, relateNumber)
            : result;
    }

    /// <summary>
    /// 即時取回開立發票 (C0403) Status，用 sellerIdentifier + relateNumber 查 invoiceNumber。
    /// 因為 Gateweb 是異步上傳，uploadStatus=P (Pending) 時要 poll 等到 Y/C/N 才視為完成。
    /// </summary>
    private async Task<string?> QueryInvoiceNumberAsync(string sellerIdentifier, string relateNumber, EinvoicePlatform p, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sellerIdentifier) || string.IsNullOrEmpty(relateNumber))
            return null;

        var maxAttempts = 6; // 共約 12 秒
        var delayMs = 2000;

        for (var i = 0; i < maxAttempts; i++)
        {
            try
            {
                var token = await GetTokenAsync(p, ct);
                var http = _httpClientFactory.CreateClient();
                http.Timeout = TimeSpan.FromSeconds(15);
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var url = $"{BaseUrl(p)}/api/v1/invoice/{Uri.EscapeDataString(sellerIdentifier)}/{Uri.EscapeDataString(relateNumber)}";
                var resp = await http.GetAsync(url, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Gateweb query HTTP {Status}: {Body}", (int)resp.StatusCode, body);
                    return null;
                }

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var migNumber = TryFindString(root, "migNumber") ?? TryFindString(root, "invoiceNumber");

                // 拿到 migNumber 即可返回（uploadStatus=P 是異步處理中，但 number 已 assign）
                if (!string.IsNullOrEmpty(migNumber))
                    return migNumber;

                if (i < maxAttempts - 1) await Task.Delay(delayMs, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gateweb 查詢發票號碼失敗 relate={Relate} attempt={I}", relateNumber, i);
                return null;
            }
        }
        _logger.LogWarning("Gateweb uploadStatus 仍為 Pending (relateNumber={Relate})，放棄 polling", relateNumber);
        return null;
    }

    private static string? TryFindString(JsonElement el, string key)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.String)
                    return prop.Value.GetString();
                var nested = TryFindString(prop.Value, key);
                if (!string.IsNullOrEmpty(nested)) return nested;
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                var nested = TryFindString(item, key);
                if (!string.IsNullOrEmpty(nested)) return nested;
            }
        }
        return null;
    }

    public async Task<EinvoiceProviderResult> InvalidAsync(Einvoice e, string reason, EinvoicePlatform p, CancellationToken ct = default)
    {
        // Gateweb C0503 用「原發票的 relateNumber」對應，優先用 ProviderRelateNumber（Issue 時寫入）
        var originalRelateNumber = e.ProviderRelateNumber ?? ExtractOriginalRelateNumber(e.Note) ?? e.InvoiceNumber;
        if (string.IsNullOrEmpty(originalRelateNumber))
            return EinvoiceProviderResult.Fail("缺少原發票 relateNumber");

        var body = new Dictionary<string, object?>
        {
            ["sellerId"] = TryReadExtra(p.ExtraParams, "sellerIdentifier") ?? e.SellerTaxId ?? string.Empty,
            ["relateNumber"] = originalRelateNumber,
            ["cancelReason"] = string.IsNullOrEmpty(reason) ? "客戶取消" : reason,
            ["cancelDate"] = DateTime.Now.ToString("yyyyMMdd"),
            ["cancelTime"] = DateTime.Now.ToString("HH:mm:ss")
        };

        var url = $"{BaseUrl(p)}/api/v1/simplified/C0503?domestic=true&companyKey={Uri.EscapeDataString(p.MerchantId!)}";
        var json = await PostWithAuthAsync(url, body, p, ct);
        return ParseResult(json);
    }

    private static string? ExtractOriginalRelateNumber(string? note)
    {
        if (string.IsNullOrEmpty(note) || !note.StartsWith("ORDERNO=")) return null;
        var idx = note.IndexOf(';');
        return idx > 0 ? note.Substring(8, idx - 8) : note.Substring(8);
    }

    public async Task<EinvoiceProviderResult> AllowanceAsync(EinvoiceAllowance a, Einvoice e, EinvoicePlatform p, CancellationToken ct = default)
    {
        // Gateweb D0403 用 originalRelateNumber + relateNumber + sellerId
        var originalRelateNumber = e.ProviderRelateNumber ?? ExtractOriginalRelateNumber(a.Note ?? e.Note) ?? e.InvoiceNumber;
        if (string.IsNullOrEmpty(originalRelateNumber))
            return EinvoiceProviderResult.Fail("缺少原發票 relateNumber");

        // 折讓單自身的 relateNumber 限 16 字元
        var allowanceRelateNumber = $"ALW{a.Id:D6}{DateTime.UtcNow:HHmmss}";
        if (allowanceRelateNumber.Length > 16) allowanceRelateNumber = allowanceRelateNumber[..16];

        var details = a.Items.Select((it, idx) => new Dictionary<string, object?>
        {
            ["amount"] = (int)Math.Round(e.TaxType == "taxable" ? it.Subtotal / 1.05m : it.Subtotal), // 不含稅
            ["description"] = it.Description,
            ["quantity"] = it.Quantity,
            ["unit"] = "個",
            ["sequenceNumber"] = (idx + 1).ToString("D3"),
            ["tax"] = e.TaxType == "taxable" ? (int)Math.Round(it.Subtotal - it.Subtotal / 1.05m) : 0,
            ["taxType"] = MapTaxType(e.TaxType),
            ["unitPrice"] = (int)Math.Round(e.TaxType == "taxable" ? it.UnitPrice / 1.05m : it.UnitPrice)
        }).ToArray();

        var body = new Dictionary<string, object?>
        {
            ["detail"] = details,
            ["originalRelateNumber"] = originalRelateNumber,
            ["relateNumber"] = allowanceRelateNumber,
            ["allowanceDate"] = a.AllowanceDate.ToString("yyyyMMdd"),
            ["sellerId"] = TryReadExtra(p.ExtraParams, "sellerIdentifier") ?? e.SellerTaxId ?? string.Empty
        };

        var url = $"{BaseUrl(p)}/api/v1/simplified/D0403?domestic=true&companyKey={Uri.EscapeDataString(p.MerchantId!)}";
        var json = await PostWithAuthAsync(url, body, p, ct);
        var result = ParseResult(json);
        // 若成功，把折讓單自己的 relateNumber 當作 number 回傳
        return result.Success ? EinvoiceProviderResult.Ok(allowanceRelateNumber, result.Code, result.Message, result.RawResponse) : result;
    }

    public async Task<EinvoiceProviderResult> AllowanceInvalidAsync(EinvoiceAllowance a, string reason, EinvoicePlatform p, CancellationToken ct = default)
    {
        // D0503 需要 originalRelateNumber + relateNumber(折讓單自己)
        var originalRelateNumber = a.Einvoice?.ProviderRelateNumber ?? ExtractOriginalRelateNumber(a.Note ?? a.Einvoice?.Note) ?? a.Einvoice?.InvoiceNumber;
        if (string.IsNullOrEmpty(originalRelateNumber))
            return EinvoiceProviderResult.Fail("缺少原發票 relateNumber");
        if (string.IsNullOrEmpty(a.AllowanceNumber))
            return EinvoiceProviderResult.Fail("尚未取得折讓單號");

        var body = new Dictionary<string, object?>
        {
            ["cancelDate"] = DateTime.Now.ToString("yyyyMMdd"),
            ["cancelTime"] = DateTime.Now.ToString("HH:mm:ss"),
            ["cancelReason"] = string.IsNullOrEmpty(reason) ? "折讓取消" : reason,
            ["originalRelateNumber"] = originalRelateNumber,
            ["relateNumber"] = a.AllowanceNumber,
            ["sellerId"] = TryReadExtra(p.ExtraParams, "sellerIdentifier") ?? a.Einvoice?.SellerTaxId ?? string.Empty
        };

        var url = $"{BaseUrl(p)}/api/v1/simplified/D0503?domestic=true&companyKey={Uri.EscapeDataString(p.MerchantId!)}";
        var json = await PostWithAuthAsync(url, body, p, ct);
        return ParseResult(json);
    }

    // === Helpers ===

    private async Task<string> GetTokenAsync(EinvoicePlatform p, CancellationToken ct)
    {
        if (TokenCache.TryGetValue(p.Id, out var cached) && cached.ExpireAt > DateTime.UtcNow)
            return cached.Token;

        var http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(15);
        var url = $"{BaseUrl(p)}/api/authenticate";
        var resp = await http.PostAsJsonAsync(url, new { username = p.ApiKey, password = p.ApiSecret, rememberMe = true }, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"Gateweb 認證失敗 HTTP {(int)resp.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        var token = doc.RootElement.GetProperty("id_token").GetString()
                    ?? throw new InvalidOperationException("Gateweb 認證未回傳 id_token");
        TokenCache[p.Id] = (token, DateTime.UtcNow.Add(TokenLifetime));
        return token;
    }

    private async Task<JsonElement> PostWithAuthAsync(string url, object body, EinvoicePlatform p, CancellationToken ct)
    {
        var token = await GetTokenAsync(p, ct);
        var http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(30);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await http.PostAsJsonAsync(url, body, ct);
        var respBody = await resp.Content.ReadAsStringAsync(ct);

        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // token 過期 → 重取一次
            TokenCache.TryRemove(p.Id, out _);
            token = await GetTokenAsync(p, ct);
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            resp = await http.PostAsJsonAsync(url, body, ct);
            respBody = await resp.Content.ReadAsStringAsync(ct);
        }

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Gateweb HTTP {Status}: {Body}", (int)resp.StatusCode, respBody);
            throw new HttpRequestException($"Gateweb HTTP {(int)resp.StatusCode}: {respBody}");
        }

        return JsonDocument.Parse(respBody).RootElement.Clone();
    }

    private EinvoiceProviderResult ParseResult(JsonElement json, string? numberField = null)
    {
        var raw = json.GetRawText();
        string? code = null;
        string? msg = null;

        // Gateweb 標準回應有 typeCode + errors[]；typeCode=0 為成功
        if (json.TryGetProperty("typeCode", out var tc)) code = tc.ToString();
        if (json.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array && errs.GetArrayLength() > 0)
        {
            var first = errs[0];
            if (first.TryGetProperty("errorMessage", out var em)) msg = em.GetString();
        }
        if (json.TryGetProperty("status", out var s) && code == null) code = s.ToString();
        if (json.TryGetProperty("message", out var m) && msg == null) msg = m.GetString();

        var success = code switch
        {
            "0" or "200" or "S" or "true" or "True" => true,
            _ => json.TryGetProperty("success", out var sc) && sc.ValueKind == JsonValueKind.True
        };

        if (!success)
            return EinvoiceProviderResult.Fail(msg ?? "Gateweb 回傳失敗", code, raw);

        if (numberField == null) return EinvoiceProviderResult.Ok(null, code, msg, raw);

        // 嘗試從 data 或頂層取單號
        if (json.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty(numberField, out var n1))
            return EinvoiceProviderResult.Ok(n1.GetString(), code, msg, raw);
        if (json.TryGetProperty(numberField, out var n2))
            return EinvoiceProviderResult.Ok(n2.GetString(), code, msg, raw);

        return EinvoiceProviderResult.Ok(null, code, msg, raw);
    }

    private static string? TryReadExtra(string? extraJson, string key)
    {
        if (string.IsNullOrEmpty(extraJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(extraJson);
            if (doc.RootElement.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        }
        catch { }
        return null;
    }

    private static string MapTaxType(string sysTaxType) => sysTaxType switch
    {
        "taxable" => "1",
        "zero" => "2",
        "free" => "3",
        _ => "1"
    };

    private static string MapCarrierType(string? sys) => sys switch
    {
        "mobile" => "3J0002",
        "citizen" => "CQ0001",
        "member" => "EJ0110",
        _ => string.Empty
    };
}

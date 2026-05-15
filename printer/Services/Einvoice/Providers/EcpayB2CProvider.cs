using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using printer.Data.Entities;

namespace printer.Services.Invoicing.Providers;

/// <summary>
/// 綠界 ECPay B2C 電子發票串接。
/// 文件：invoice/ecpay/b2c/14-一般開立發票.md, 21-作廢發票.md, 19-一般開立折讓-紙本.md, 22-作廢折讓.md
/// </summary>
public class EcpayB2CProvider : IEinvoiceProvider
{
    public string Code => "ecpay_b2c";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EcpayB2CProvider> _logger;

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    public EcpayB2CProvider(IHttpClientFactory httpClientFactory, ILogger<EcpayB2CProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private string BaseUrl(EinvoicePlatform p) =>
        (p.IsSandbox ? "https://einvoice-stage.ecpay.com.tw" : "https://einvoice.ecpay.com.tw").TrimEnd('/');

    public async Task<EinvoiceProviderResult> IssueAsync(Einvoice e, EinvoicePlatform p, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(p.MerchantId) || string.IsNullOrEmpty(p.ApiKey) || string.IsNullOrEmpty(p.ApiSecret))
            return EinvoiceProviderResult.Fail("ECPay 設定不完整：需 MerchantID / HashKey / HashIV");

        // ECPay B2C 規則：ItemPrice / ItemAmount 為含稅，且 sum(ItemAmount) 必須等於 SalesAmount
        // 我們的 domain 是未稅（Item.UnitPrice / Item.Subtotal），這裡換算成含稅
        var multiplier = e.TaxType == "taxable" ? 1m + e.TaxRate / 100m : 1m;
        var items = e.Items.Select((it, idx) =>
        {
            var taxedPrice = (int)Math.Round(it.UnitPrice * multiplier);
            return new
            {
                ItemSeq = idx + 1,
                ItemName = it.Description,
                ItemCount = it.Quantity,
                ItemWord = "個",
                ItemPrice = taxedPrice,
                ItemTaxType = MapTaxType(e.TaxType),
                ItemAmount = taxedPrice * it.Quantity,
                ItemRemark = (string?)null
            };
        }).ToArray();
        var salesAmount = items.Sum(i => i.ItemAmount);

        var print = string.IsNullOrEmpty(e.CarrierType) || e.CarrierType == "none" ? "1" : "0";
        // Print=1 + CustomerIdentifier 有值時，載具只能是手機條碼，且不能捐贈
        var donation = e.CarrierType == "donate" ? "1" : "0";

        var data = new Dictionary<string, object?>
        {
            ["MerchantID"] = p.MerchantId,
            ["RelateNumber"] = $"INV{e.Id:D10}{DateTime.UtcNow:HHmmss}",
            ["CustomerID"] = string.Empty,
            ["CustomerIdentifier"] = e.BuyerTaxId ?? string.Empty,
            ["CustomerName"] = e.BuyerName,
            ["CustomerAddr"] = e.Partner?.Address ?? string.Empty,
            ["CustomerPhone"] = e.Partner?.Phone ?? string.Empty,
            ["CustomerEmail"] = e.BuyerEmail ?? string.Empty,
            ["ClearanceMark"] = e.TaxType == "zero" ? "1" : string.Empty,
            ["Print"] = print,
            ["Donation"] = donation,
            ["LoveCode"] = donation == "1" ? e.DonationCode ?? string.Empty : string.Empty,
            ["CarrierType"] = MapCarrierType(e.CarrierType),
            ["CarrierNum"] = string.IsNullOrEmpty(e.CarrierType) || e.CarrierType == "none" || e.CarrierType == "donate" ? string.Empty : e.CarrierId ?? string.Empty,
            ["TaxType"] = MapTaxType(e.TaxType),
            ["ZeroTaxRateReason"] = string.Empty,
            ["SpecialTaxType"] = 0,
            ["SalesAmount"] = salesAmount,
            ["TaxAmount"] = (int)Math.Round(e.TaxAmount),
            ["InvoiceRemark"] = e.Note ?? string.Empty,
            ["Items"] = items,
            ["InvType"] = "07",
            ["vat"] = "1"
        };

        var json = await PostAsync($"{BaseUrl(p)}/B2CInvoice/Issue", data, p, ct);
        return ParseResult(json, expectInvoiceNo: true);
    }

    public async Task<EinvoiceProviderResult> InvalidAsync(Einvoice e, string reason, EinvoicePlatform p, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(e.InvoiceNumber))
            return EinvoiceProviderResult.Fail("尚未取得發票號碼，無法作廢");

        var data = new Dictionary<string, object?>
        {
            ["MerchantID"] = p.MerchantId,
            ["InvoiceNo"] = e.InvoiceNumber,
            ["InvoiceDate"] = e.InvoiceDate.ToString("yyyy-MM-dd"),
            ["Reason"] = string.IsNullOrEmpty(reason) ? "客戶取消" : reason
        };

        var json = await PostAsync($"{BaseUrl(p)}/B2CInvoice/Invalid", data, p, ct);
        return ParseResult(json, expectInvoiceNo: false);
    }

    public async Task<EinvoiceProviderResult> AllowanceAsync(EinvoiceAllowance a, Einvoice e, EinvoicePlatform p, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(e.InvoiceNumber))
            return EinvoiceProviderResult.Fail("原發票尚未取得發票號碼");

        // 折讓沿用 B2C 含稅規則：ItemPrice/ItemAmount 為含稅
        var allowMultiplier = e.TaxType == "taxable" ? 1m + e.TaxRate / 100m : 1m;
        var items = a.Items.Select((it, idx) =>
        {
            var taxedPrice = (int)Math.Round(it.UnitPrice * allowMultiplier);
            return new
            {
                ItemSeq = idx + 1,
                ItemName = it.Description,
                ItemCount = it.Quantity,
                ItemWord = "個",
                ItemPrice = taxedPrice,
                ItemTaxType = MapTaxType(e.TaxType),
                ItemAmount = taxedPrice * it.Quantity,
                ItemRemark = (string?)null
            };
        }).ToArray();

        // a.AllowanceType: paper(紙本) -> /Allowance ; online(通知) -> /AllowanceByCollegiate
        var endpoint = a.AllowanceType == "online" ? "AllowanceByCollegiate" : "Allowance";
        // AllowanceNotify: S=簡訊 E=Email A=兩者 N=皆不通知
        var notify = !string.IsNullOrEmpty(e.BuyerEmail) ? "E" : "N";

        var data = new Dictionary<string, object?>
        {
            ["MerchantID"] = p.MerchantId,
            ["InvoiceNo"] = e.InvoiceNumber,
            ["InvoiceDate"] = e.InvoiceDate.ToString("yyyy-MM-dd"),
            ["AllowanceNotify"] = notify,
            ["CustomerName"] = e.BuyerName,
            ["NotifyMail"] = notify == "E" || notify == "A" ? e.BuyerEmail ?? string.Empty : string.Empty,
            ["NotifyPhone"] = string.Empty,
            ["AllowanceAmount"] = (int)Math.Round(a.TotalAmount),
            ["Items"] = items
        };

        var json = await PostAsync($"{BaseUrl(p)}/B2CInvoice/{endpoint}", data, p, ct);
        var result = ParseResult(json, expectInvoiceNo: false);
        if (result.Success)
        {
            // ECPay 回傳含 IA_Allow_No
            using var doc = JsonDocument.Parse(result.RawResponse ?? "{}");
            if (doc.RootElement.TryGetProperty("IA_Allow_No", out var no))
                return EinvoiceProviderResult.Ok(no.GetString(), result.Code, result.Message, result.RawResponse);
        }
        return result;
    }

    public async Task<EinvoiceProviderResult> AllowanceInvalidAsync(EinvoiceAllowance a, string reason, EinvoicePlatform p, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(a.AllowanceNumber))
            return EinvoiceProviderResult.Fail("尚未取得折讓單號，無法作廢");

        var data = new Dictionary<string, object?>
        {
            ["MerchantID"] = p.MerchantId,
            ["InvoiceNo"] = a.Einvoice?.InvoiceNumber ?? string.Empty,
            ["AllowanceNo"] = a.AllowanceNumber,
            ["Reason"] = string.IsNullOrEmpty(reason) ? "折讓取消" : reason
        };

        var json = await PostAsync($"{BaseUrl(p)}/B2CInvoice/AllowanceInvalid", data, p, ct);
        return ParseResult(json, expectInvoiceNo: false);
    }

    // === Helpers ===

    private async Task<JsonElement> PostAsync(string url, Dictionary<string, object?> data, EinvoicePlatform p, CancellationToken ct)
    {
        var dataJson = JsonSerializer.Serialize(data, JsonOpt);
        _logger.LogInformation("ECPay B2C 送出 data: {Json}", dataJson);
        var encryptedData = EcpayCrypto.Encrypt(dataJson, p.ApiKey!, p.ApiSecret!);
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var envelope = new
        {
            PlatformID = (string?)null,
            MerchantID = p.MerchantId,
            RqHeader = new { Timestamp = ts },
            Data = encryptedData
        };

        var http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(30);
        var resp = await http.PostAsJsonAsync(url, envelope, JsonOpt, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("ECPay B2C HTTP {Status}: {Body}", (int)resp.StatusCode, body);
            throw new HttpRequestException($"ECPay HTTP {(int)resp.StatusCode}: {body}");
        }

        using var envDoc = JsonDocument.Parse(body);
        var root = envDoc.RootElement.Clone();

        // 解 Data
        if (root.TryGetProperty("Data", out var dataProp) && dataProp.ValueKind == JsonValueKind.String)
        {
            var encryptedResp = dataProp.GetString();
            if (!string.IsNullOrEmpty(encryptedResp))
            {
                var decrypted = EcpayCrypto.Decrypt(encryptedResp, p.ApiKey!, p.ApiSecret!);
                using var inner = JsonDocument.Parse(decrypted);
                return inner.RootElement.Clone();
            }
        }
        return root;
    }

    private EinvoiceProviderResult ParseResult(JsonElement json, bool expectInvoiceNo)
    {
        var raw = json.GetRawText();
        string? code = null;
        string? msg = null;

        if (json.TryGetProperty("RtnCode", out var rc)) code = rc.ToString();
        if (json.TryGetProperty("RtnMsg", out var rm)) msg = rm.GetString();

        if (code == "1")
        {
            string? number = null;
            if (expectInvoiceNo && json.TryGetProperty("InvoiceNo", out var inv))
                number = inv.GetString();

            // 解析 ECPay 回傳的開立日期（格式可能是 "2026-05-15 15:30:00" 或 "2026-05-15"）
            DateOnly? issueDate = null;
            if (json.TryGetProperty("InvoiceDate", out var dateEl) && dateEl.ValueKind == JsonValueKind.String)
            {
                var s = dateEl.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    var datePart = s.Split(' ', 'T')[0];
                    if (DateOnly.TryParse(datePart, out var d)) issueDate = d;
                }
            }
            return EinvoiceProviderResult.Ok(number, code, msg, raw, issueDate);
        }

        return EinvoiceProviderResult.Fail(msg ?? "ECPay 回傳失敗", code, raw);
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
        "mobile" => "3",     // 手機條碼
        "citizen" => "2",    // 自然人憑證
        "member" => "1",     // 會員載具
        _ => string.Empty    // 紙本/捐贈不帶
    };
}

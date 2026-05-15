using System.Net.Http.Json;
using System.Text.Json;
using printer.Data.Entities;

namespace printer.Services.Invoicing.Providers;

/// <summary>
/// 綠界 ECPay B2B 電子發票串接（交換模式）。
/// 文件：invoice/綠界 ECPay/b2b/12-開立發票.md, 14-作廢發票.md, 18-開立折讓發票.md, 20-作廢折讓發票.md
/// 共用 AES 加密與 Envelope 與 B2C 相同，差異在 endpoint 與 B2B 強制買方統編。
/// </summary>
public class EcpayB2BProvider : IEinvoiceProvider
{
    public string Code => "ecpay_b2b";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EcpayB2BProvider> _logger;

    private static readonly JsonSerializerOptions JsonOpt = new() { PropertyNamingPolicy = null };

    public EcpayB2BProvider(IHttpClientFactory httpClientFactory, ILogger<EcpayB2BProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private string BaseUrl(EinvoicePlatform p) =>
        (p.IsSandbox ? "https://einvoice-stage.ecpay.com.tw" : "https://einvoice.ecpay.com.tw").TrimEnd('/');

    public async Task<EinvoiceProviderResult> IssueAsync(Einvoice e, EinvoicePlatform p, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(p.MerchantId) || string.IsNullOrEmpty(p.ApiKey) || string.IsNullOrEmpty(p.ApiSecret))
            return EinvoiceProviderResult.Fail("ECPay B2B 設定不完整：需 MerchantID / HashKey / HashIV");
        if (string.IsNullOrEmpty(e.BuyerTaxId))
            return EinvoiceProviderResult.Fail("B2B 發票必須帶買方統編");

        // ECPay B2B 規則：ItemPrice / ItemAmount 為未稅、ItemTax 為該項稅額
        // 我們的 domain 本來就是未稅（Item.UnitPrice/Subtotal）→ 直接送，不必再除稅
        // SalesAmount 必須等於 sum(ItemAmount)，TaxAmount 必須等於 sum(ItemTax)
        var items = e.Items.Select((it, idx) =>
        {
            var itemAmount = (int)Math.Round(it.Subtotal);
            var itemTax = e.TaxType == "taxable" ? (int)Math.Round(it.Subtotal * e.TaxRate / 100m) : 0;
            return new
            {
                ItemSeq = idx + 1,
                ItemName = it.Description,
                ItemCount = it.Quantity,
                ItemWord = "個",
                ItemPrice = (int)Math.Round(it.UnitPrice),
                ItemAmount = itemAmount,
                ItemTax = itemTax,
                ItemRemark = (string?)null
            };
        }).ToArray();
        var sumItemAmount = items.Sum(i => i.ItemAmount);
        var sumItemTax = items.Sum(i => i.ItemTax);

        var data = new Dictionary<string, object?>
        {
            ["MerchantID"] = p.MerchantId,
            ["RelateNumber"] = $"INV{e.Id:D6}{DateTime.UtcNow:HHmmss}",
            ["CustomerIdentifier"] = e.BuyerTaxId,
            ["CustomerEmail"] = e.BuyerEmail ?? string.Empty,
            ["CustomerAddress"] = (string?)null,
            ["CustomerTelephoneNumber"] = (string?)null,
            ["ClearanceMark"] = e.TaxType == "zero" ? "1" : (string?)null,
            ["InvType"] = "07",
            ["TaxType"] = MapTaxType(e.TaxType),
            ["TaxRate"] = e.TaxType == "taxable" ? 0.05 : 0.0,
            ["Items"] = items,
            ["SalesAmount"] = sumItemAmount,
            ["TaxAmount"] = sumItemTax,
            ["TotalAmount"] = sumItemAmount + sumItemTax,
            ["InvoiceRemark"] = e.Note ?? string.Empty
        };

        var json = await PostAsync($"{BaseUrl(p)}/B2BInvoice/Issue", data, p, ct);
        return ParseResult(json, "InvoiceNumber");
    }

    public async Task<EinvoiceProviderResult> InvalidAsync(Einvoice e, string reason, EinvoicePlatform p, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(e.InvoiceNumber))
            return EinvoiceProviderResult.Fail("尚未取得發票號碼");

        var data = new Dictionary<string, object?>
        {
            ["MerchantID"] = p.MerchantId,
            ["InvoiceNumber"] = e.InvoiceNumber,
            ["InvoiceDate"] = e.InvoiceDate.ToString("yyyy-MM-dd"),
            ["Reason"] = string.IsNullOrEmpty(reason) ? "客戶取消" : reason,
            ["Remark"] = string.Empty
        };

        var json = await PostAsync($"{BaseUrl(p)}/B2BInvoice/Invalid", data, p, ct);
        return ParseResult(json);
    }

    public async Task<EinvoiceProviderResult> AllowanceAsync(EinvoiceAllowance a, Einvoice e, EinvoicePlatform p, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(e.InvoiceNumber))
            return EinvoiceProviderResult.Fail("原發票尚未取得發票號碼");

        // B2B 折讓 Details：ItemPrice/ItemAmount 為未稅、Tax 為該項稅額（domain 已是未稅，直接送）
        var details = a.Items.Select((it, idx) =>
        {
            var amt = (int)Math.Round(it.Subtotal);
            var tax = e.TaxType == "taxable" ? (int)Math.Round(it.Subtotal * e.TaxRate / 100m) : 0;
            return new
            {
                OriginalInvoiceNumber = e.InvoiceNumber,
                OriginalInvoiceDate = e.InvoiceDate.ToString("yyyy-MM-dd"),
                OriginalSequenceNumber = idx + 1,
                ItemName = it.Description,
                ItemCount = it.Quantity,
                ItemPrice = (int)Math.Round(it.UnitPrice),
                ItemAmount = amt,
                Tax = tax
            };
        }).ToArray();

        // ECPay B2B Allowance：TotalAmount 為**未稅**（= sum of Details.ItemAmount）；TaxAmount 為稅額總計
        var untaxedTotal = details.Sum(d => d.ItemAmount);
        var totalTax = details.Sum(d => d.Tax);

        var data = new Dictionary<string, object?>
        {
            ["MerchantID"] = p.MerchantId,
            ["AllowanceDate"] = a.AllowanceDate.ToDateTime(TimeOnly.FromDateTime(DateTime.Now)).ToString("yyyy-MM-dd HH:mm:ss"),
            ["CustomerEmail"] = e.BuyerEmail ?? string.Empty,
            ["CustomerAddress"] = e.Partner?.Address ?? string.Empty,
            ["TaxType"] = MapTaxType(e.TaxType),
            ["TaxRate"] = e.TaxType == "taxable" ? 0.05 : 0.0,
            ["ClearanceMark"] = e.TaxType == "zero" ? "1" : (string?)null,
            ["TaxAmount"] = totalTax,
            ["TotalAmount"] = untaxedTotal,
            ["Details"] = details
        };

        var json = await PostAsync($"{BaseUrl(p)}/B2BInvoice/Allowance", data, p, ct);
        return ParseResult(json, "AllowanceNo");
    }

    public async Task<EinvoiceProviderResult> AllowanceInvalidAsync(EinvoiceAllowance a, string reason, EinvoicePlatform p, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(a.AllowanceNumber))
            return EinvoiceProviderResult.Fail("尚未取得折讓單號");

        var data = new Dictionary<string, object?>
        {
            ["MerchantID"] = p.MerchantId,
            ["AllowanceNo"] = a.AllowanceNumber,
            ["Reason"] = string.IsNullOrEmpty(reason) ? "折讓取消" : reason,
            ["Remark"] = string.Empty
        };

        var json = await PostAsync($"{BaseUrl(p)}/B2BInvoice/CancelAllowance", data, p, ct);
        return ParseResult(json);
    }

    /// <summary>
    /// 交易對象維護：在 ECPay 後台新增/更新「買方」「賣方」統編。
    /// B2B 開立前必須先在系統中存在對應的交易對象。
    /// </summary>
    public async Task<EinvoiceProviderResult> RegisterCustomerAsync(string identifier, string companyName, string emailAddress, EinvoicePlatform p, string action = "Add", string type = "1", string exchangeMode = "0", CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(p.MerchantId) || string.IsNullOrEmpty(p.ApiKey) || string.IsNullOrEmpty(p.ApiSecret))
            return EinvoiceProviderResult.Fail("ECPay B2B 設定不完整");

        var data = new Dictionary<string, object?>
        {
            ["MerchantID"] = p.MerchantId,
            ["Action"] = action,
            ["Identifier"] = identifier,
            ["type"] = type,
            ["CompanyName"] = companyName,
            ["TradingSlang"] = "TEST",
            ["ExchangeMode"] = exchangeMode,
            ["EmailAddress"] = emailAddress
        };

        var json = await PostAsync($"{BaseUrl(p)}/B2BInvoice/MaintainMerchantCustomerData", data, p, ct);
        return ParseResult(json);
    }

    // === Helpers ===

    private async Task<JsonElement> PostAsync(string url, Dictionary<string, object?> data, EinvoicePlatform p, CancellationToken ct)
    {
        var dataJson = JsonSerializer.Serialize(data, JsonOpt);
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
            _logger.LogWarning("ECPay B2B HTTP {Status}: {Body}", (int)resp.StatusCode, body);
            throw new HttpRequestException($"ECPay B2B HTTP {(int)resp.StatusCode}: {body}");
        }

        using var envDoc = JsonDocument.Parse(body);
        var root = envDoc.RootElement.Clone();

        if (root.TryGetProperty("Data", out var dataProp) && dataProp.ValueKind == JsonValueKind.String)
        {
            var decrypted = EcpayCrypto.Decrypt(dataProp.GetString()!, p.ApiKey!, p.ApiSecret!);
            return JsonDocument.Parse(decrypted).RootElement.Clone();
        }
        return root;
    }

    private EinvoiceProviderResult ParseResult(JsonElement json, string? numberField = null)
    {
        var raw = json.GetRawText();
        string? code = null;
        string? msg = null;
        if (json.TryGetProperty("RtnCode", out var rc)) code = rc.ToString();
        if (json.TryGetProperty("RtnMsg", out var rm)) msg = rm.GetString();

        if (code == "1")
        {
            string? number = null;
            if (numberField != null && json.TryGetProperty(numberField, out var no))
                number = no.GetString();

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
        return EinvoiceProviderResult.Fail(msg ?? "ECPay B2B 回傳失敗", code, raw);
    }

    private static string MapTaxType(string sysTaxType) => sysTaxType switch
    {
        "taxable" => "1",
        "zero" => "2",
        "free" => "3",
        _ => "1"
    };
}

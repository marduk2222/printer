using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using printer.Data.Entities;

namespace printer.Services.Invoicing.Providers;

/// <summary>
/// 藍新 ezPay 電子發票串接。
/// 文件：invoice/藍新 ezPay/api-spec.md
/// 流程：欄位陣列 → http_build_query → PKCS#7(32) → AES-256-CBC → 小寫 hex → POST {MerchantID_, PostData_}
/// </summary>
public class EzpayProvider : IEinvoiceProvider
{
    public string Code => "ezpay";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EzpayProvider> _logger;

    public EzpayProvider(IHttpClientFactory httpClientFactory, ILogger<EzpayProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private string BaseUrl(EinvoicePlatform p) =>
        p.IsSandbox ? "https://cinv.ezpay.com.tw" : "https://inv.ezpay.com.tw";

    public async Task<EinvoiceProviderResult> IssueAsync(Einvoice e, EinvoicePlatform p, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(p.MerchantId) || string.IsNullOrEmpty(p.ApiKey) || string.IsNullOrEmpty(p.ApiSecret))
            return EinvoiceProviderResult.Fail("ezPay 設定不完整：需 MerchantID / HashKey(32) / HashIV(16)");

        var orderNo = $"INV{e.Id:D6}{DateTime.UtcNow:HHmmss}";
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["RespondType"] = "JSON",
            ["Version"] = "1.5",
            ["TimeStamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            ["MerchantOrderNo"] = orderNo,
            ["Status"] = "1", // 即時開立
            ["Category"] = string.IsNullOrEmpty(e.BuyerTaxId) ? "B2C" : "B2B",
            ["BuyerName"] = e.BuyerName,
            ["BuyerEmail"] = e.BuyerEmail ?? string.Empty,
            ["PrintFlag"] = (string.IsNullOrEmpty(e.CarrierType) || e.CarrierType == "none") ? "Y" : "N",
            ["TaxType"] = MapTaxType(e.TaxType),
            ["TaxRate"] = e.TaxType == "taxable" ? e.TaxRate.ToString() : "0",
            ["Amt"] = ((int)Math.Round(e.Amount)).ToString(),
            ["TaxAmt"] = ((int)Math.Round(e.TaxAmount)).ToString(),
            ["TotalAmt"] = ((int)Math.Round(e.TotalAmount)).ToString(),
            ["ItemName"] = string.Join("|", e.Items.Select(i => i.Description)),
            ["ItemCount"] = string.Join("|", e.Items.Select(i => i.Quantity)),
            ["ItemUnit"] = string.Join("|", e.Items.Select(_ => "個")),
            ["ItemPrice"] = string.Join("|", e.Items.Select(i => ((int)Math.Round(i.UnitPrice)).ToString())),
            ["ItemAmt"] = string.Join("|", e.Items.Select(i => ((int)Math.Round(i.Subtotal)).ToString())),
            ["Comment"] = e.Note ?? string.Empty
        };

        if (!string.IsNullOrEmpty(e.BuyerTaxId)) fields["BuyerUBN"] = e.BuyerTaxId;

        // 零稅率必填海關標記：1=非經海關（service export），2=經海關
        if (e.TaxType == "zero") fields["CustomsClearance"] = "1";

        if (e.CarrierType == "mobile") { fields["CarrierType"] = "0"; fields["CarrierNum"] = e.CarrierId ?? string.Empty; }
        else if (e.CarrierType == "citizen") { fields["CarrierType"] = "1"; fields["CarrierNum"] = e.CarrierId ?? string.Empty; }
        else if (e.CarrierType == "donate") { fields["LoveCode"] = e.DonationCode ?? string.Empty; }

        var json = await PostAsync($"{BaseUrl(p)}/Api/invoice_issue", fields, p, ct);
        var result = ParseResult(json, "InvoiceNumber");
        return result.Success
            ? EinvoiceProviderResult.Ok(result.Number, result.Code, result.Message, result.RawResponse, result.IssueDate, orderNo)
            : result;
    }

    public async Task<EinvoiceProviderResult> InvalidAsync(Einvoice e, string reason, EinvoicePlatform p, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(e.InvoiceNumber))
            return EinvoiceProviderResult.Fail("尚未取得發票號碼");

        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["RespondType"] = "JSON",
            ["Version"] = "1.0",
            ["TimeStamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            ["InvoiceNumber"] = e.InvoiceNumber,
            ["InvalidReason"] = string.IsNullOrEmpty(reason) ? "客戶取消" : reason
        };

        var json = await PostAsync($"{BaseUrl(p)}/Api/invoice_invalid", fields, p, ct);
        return ParseResult(json);
    }

    public async Task<EinvoiceProviderResult> AllowanceAsync(EinvoiceAllowance a, Einvoice e, EinvoicePlatform p, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(e.InvoiceNumber))
            return EinvoiceProviderResult.Fail("原發票尚未取得發票號碼");

        // 各 item 稅額：item.Subtotal × rate（必須與 TotalAmt 對得上，否則 ezPay 回「折讓單加總金額與系統計算不符」）
        var rate = e.TaxType == "taxable" ? e.TaxRate / 100m : 0m;
        var taxAmtPerItem = a.Items.Select(i => (int)Math.Round(i.Subtotal * rate)).ToArray();
        // 原發票 MerchantOrderNo：優先取 einvoice.ProviderRelateNumber（Issue 時寫入）；
        // 其次 fallback 至 a.Note 的舊「ORDERNO=」格式（向後相容）
        var originalOrderNo = e.ProviderRelateNumber;
        if (string.IsNullOrEmpty(originalOrderNo) && !string.IsNullOrEmpty(a.Note) && a.Note.StartsWith("ORDERNO="))
        {
            var idx = a.Note.IndexOf(';');
            originalOrderNo = idx > 0 ? a.Note.Substring(8, idx - 8) : a.Note.Substring(8);
        }
        if (string.IsNullOrEmpty(originalOrderNo))
            originalOrderNo = $"INV{e.Id:D6}";

        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["RespondType"] = "JSON",
            ["Version"] = "1.3",
            ["TimeStamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            ["InvoiceNo"] = e.InvoiceNumber,
            ["MerchantOrderNo"] = originalOrderNo,
            ["ItemName"] = string.Join("|", a.Items.Select(i => i.Description)),
            ["ItemCount"] = string.Join("|", a.Items.Select(i => i.Quantity)),
            ["ItemUnit"] = string.Join("|", a.Items.Select(_ => "個")),
            ["ItemPrice"] = string.Join("|", a.Items.Select(i => ((int)Math.Round(i.UnitPrice)).ToString())),
            ["ItemAmt"] = string.Join("|", a.Items.Select(i => ((int)Math.Round(i.Subtotal)).ToString())),
            ["ItemTaxAmt"] = string.Join("|", taxAmtPerItem),
            ["TotalAmt"] = ((int)Math.Round(a.TotalAmount)).ToString(),
            ["BuyerEmail"] = e.BuyerEmail ?? string.Empty,
            ["Status"] = "1" // 立即確認
        };

        var json = await PostAsync($"{BaseUrl(p)}/Api/allowance_issue", fields, p, ct);
        return ParseResult(json, "AllowanceNo");
    }

    public async Task<EinvoiceProviderResult> AllowanceInvalidAsync(EinvoiceAllowance a, string reason, EinvoicePlatform p, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(a.AllowanceNumber))
            return EinvoiceProviderResult.Fail("尚未取得折讓單號");

        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["RespondType"] = "JSON",
            ["Version"] = "1.0",
            ["TimeStamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            ["AllowanceNo"] = a.AllowanceNumber,
            ["InvalidReason"] = string.IsNullOrEmpty(reason) ? "折讓取消" : reason
        };

        var json = await PostAsync($"{BaseUrl(p)}/Api/allowanceInvalid", fields, p, ct);
        return ParseResult(json);
    }

    // === Helpers ===

    private async Task<JsonElement> PostAsync(string url, IDictionary<string, string> fields, EinvoicePlatform p, CancellationToken ct)
    {
        var query = string.Join("&", fields.Select(kv => $"{HttpUtility.UrlEncode(kv.Key)}={HttpUtility.UrlEncode(kv.Value)}"));
        _logger.LogWarning("ezPay query: {Query}", query);
        var encrypted = EncryptHex(query, p.ApiKey!, p.ApiSecret!);
        _logger.LogWarning("ezPay cipher (hex {Len}): {Cipher}", encrypted.Length, encrypted);

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("MerchantID_", p.MerchantId!),
            new KeyValuePair<string, string>("PostData_", encrypted)
        });

        var http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(30);
        var resp = await http.PostAsync(url, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("ezPay HTTP {Status}: {Body}", (int)resp.StatusCode, body);
            throw new HttpRequestException($"ezPay HTTP {(int)resp.StatusCode}: {body}");
        }

        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>本地 round-trip 自測，確認加解密邏輯正確</summary>
    public static (string cipher, string roundTrip, bool ok) SelfTest(string plain, string key, string iv)
    {
        var cipher = EncryptHex(plain, key, iv);
        var rt = DecryptHex(cipher, key, iv);
        return (cipher, rt, rt == plain);
    }

    private static string DecryptHex(string hex, string key, string iv)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var ivBytes = Encoding.UTF8.GetBytes(iv);
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.KeySize = 256; aes.BlockSize = 128;
        aes.Key = keyBytes; aes.IV = ivBytes;
        var cipher = Convert.FromHexString(hex);
        using var dec = aes.CreateDecryptor();
        var plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
        // strip PKCS7 (16-byte block)
        var padLen = plain[^1];
        if (padLen > 0 && padLen <= 16)
            return Encoding.UTF8.GetString(plain, 0, plain.Length - padLen);
        return Encoding.UTF8.GetString(plain);
    }

    private static string EncryptHex(string plain, string key, string iv)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var ivBytes = Encoding.UTF8.GetBytes(iv);
        if (keyBytes.Length != 32) throw new InvalidOperationException("ezPay HashKey 必須為 32 字元");
        if (ivBytes.Length != 16) throw new InvalidOperationException("ezPay HashIV 必須為 16 字元");

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7; // .NET 內建 16-byte block PKCS7
        aes.KeySize = 256;
        aes.BlockSize = 128;
        aes.Key = keyBytes;
        aes.IV = ivBytes;

        var data = Encoding.UTF8.GetBytes(plain);
        using var enc = aes.CreateEncryptor();
        var cipher = enc.TransformFinalBlock(data, 0, data.Length);
        return Convert.ToHexString(cipher).ToLowerInvariant();
    }

    private EinvoiceProviderResult ParseResult(JsonElement env, string? numberField = null)
    {
        var raw = env.GetRawText();
        var status = env.TryGetProperty("Status", out var s) ? s.GetString() : null;
        var msg = env.TryGetProperty("Message", out var m) ? m.GetString() : null;

        if (status != "SUCCESS")
            return EinvoiceProviderResult.Fail(msg ?? "ezPay 回傳失敗", status, raw);

        if (numberField == null)
            return EinvoiceProviderResult.Ok(null, status, msg, raw);

        if (env.TryGetProperty("Result", out var resultProp) && resultProp.ValueKind == JsonValueKind.String)
        {
            var inner = JsonDocument.Parse(resultProp.GetString()!).RootElement;
            if (inner.TryGetProperty(numberField, out var no))
                return EinvoiceProviderResult.Ok(no.GetString(), status, msg, raw);
        }
        return EinvoiceProviderResult.Ok(null, status, msg, raw);
    }

    private static string MapTaxType(string sysTaxType) => sysTaxType switch
    {
        "taxable" => "1",
        "zero" => "2",
        "free" => "3",
        _ => "1"
    };
}

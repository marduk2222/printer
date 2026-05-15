using printer.Data.Entities;

namespace printer.Services.Invoicing.Providers;

/// <summary>
/// 綠界 ECPay 統一 provider：
/// 依 Einvoice.InvoiceType 顯式 dispatch（"B2B" → B2B，其餘 → B2C）。
/// 若 InvoiceType 為空（舊資料）則 fallback 為 BuyerTaxId 規則：有統編=B2B、無=B2C。
/// 折讓/作廢折讓依「原發票的 InvoiceType」決定路徑，與開立時保持一致。
/// </summary>
public class EcpayProvider : IEinvoiceProvider
{
    public string Code => "ecpay";

    private readonly EcpayB2CProvider _b2c;
    private readonly EcpayB2BProvider _b2b;

    public EcpayProvider(EcpayB2CProvider b2c, EcpayB2BProvider b2b)
    {
        _b2c = b2c;
        _b2b = b2b;
    }

    private bool IsB2B(Einvoice? e)
    {
        if (e == null) return false;
        if (!string.IsNullOrEmpty(e.InvoiceType))
            return string.Equals(e.InvoiceType, "B2B", StringComparison.OrdinalIgnoreCase);
        return !string.IsNullOrEmpty(e.BuyerTaxId);
    }

    public Task<EinvoiceProviderResult> IssueAsync(Einvoice e, EinvoicePlatform p, CancellationToken ct = default)
        => IsB2B(e) ? _b2b.IssueAsync(e, p, ct) : _b2c.IssueAsync(e, p, ct);

    public Task<EinvoiceProviderResult> InvalidAsync(Einvoice e, string reason, EinvoicePlatform p, CancellationToken ct = default)
        => IsB2B(e) ? _b2b.InvalidAsync(e, reason, p, ct) : _b2c.InvalidAsync(e, reason, p, ct);

    public Task<EinvoiceProviderResult> AllowanceAsync(EinvoiceAllowance a, Einvoice e, EinvoicePlatform p, CancellationToken ct = default)
        => IsB2B(e) ? _b2b.AllowanceAsync(a, e, p, ct) : _b2c.AllowanceAsync(a, e, p, ct);

    public Task<EinvoiceProviderResult> AllowanceInvalidAsync(EinvoiceAllowance a, string reason, EinvoicePlatform p, CancellationToken ct = default)
        => IsB2B(a.Einvoice) ? _b2b.AllowanceInvalidAsync(a, reason, p, ct) : _b2c.AllowanceInvalidAsync(a, reason, p, ct);
}

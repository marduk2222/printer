using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;

namespace printer.Services.Invoicing;

public class EinvoiceProviderFactory : IEinvoiceProviderFactory
{
    private readonly IEnumerable<IEinvoiceProvider> _providers;
    private readonly PrinterDbContext _context;

    public EinvoiceProviderFactory(IEnumerable<IEinvoiceProvider> providers, PrinterDbContext context)
    {
        _providers = providers;
        _context = context;
    }

    public IEinvoiceProvider Get(string platformCode)
    {
        var p = _providers.FirstOrDefault(x => string.Equals(x.Code, platformCode, StringComparison.OrdinalIgnoreCase));
        if (p == null)
            throw new InvalidOperationException($"未註冊的發票平台 provider: {platformCode}");
        return p;
    }

    public async Task<(IEinvoiceProvider Provider, EinvoicePlatform Platform)?> GetActiveAsync(CancellationToken ct = default)
    {
        var platform = await _context.EinvoicePlatforms.FirstOrDefaultAsync(p => p.IsActive, ct);
        if (platform == null) return null;
        return (Get(platform.Code), platform);
    }
}

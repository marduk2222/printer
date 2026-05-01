using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Models.Dto;

namespace printer.Services.Impl;

/// <summary>
/// 品牌服務實作
/// </summary>
public class BrandService : IBrandService
{
    private readonly PrinterDbContext _context;
    private readonly ILogger<BrandService> _logger;

    public BrandService(PrinterDbContext context, ILogger<BrandService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<BrandDto>> GetBrandsAsync()
    {
        var brands = await _context.Brands
            .Include(b => b.Models)
            .ToListAsync();

        return brands.Select(b => new BrandDto
        {
            Id = b.Id,
            Name = b.Name,
            Description = b.Description ?? "",
            ModelCount = b.Models.Count
        }).ToList();
    }

    public async Task<List<ModelDto>> GetModelsAsync(ModelRequest request)
    {
        var query = _context.PrinterModels
            .Include(m => m.Brand)
            .Where(m => m.State == "1");

        if (request.BrandId.HasValue)
        {
            query = query.Where(m => m.BrandId == request.BrandId.Value);
        }

        if (!string.IsNullOrEmpty(request.Keyword))
        {
            var keyword = request.Keyword.ToLower();
            query = query.Where(m =>
                m.Name.ToLower().Contains(keyword) ||
                (m.Brand != null && m.Brand.Name.ToLower().Contains(keyword)));
        }

        var models = await query.ToListAsync();

        return models.Select(m => new ModelDto
        {
            Id = m.Id,
            Code = m.Code,
            Name = m.Name,
            BrandId = m.BrandId,
            BrandName = m.Brand?.Name ?? "",
            Feature = m.Feature ?? "",
            Description = m.Description ?? "",
            Verify = m.Verify,
            State = m.State
        }).ToList();
    }

    public async Task<List<BrandWithModelsDto>> GetBrandsWithModelsAsync()
    {
        var brands = await _context.Brands
            .Include(b => b.Models.Where(m => m.State == "1"))
            .ToListAsync();

        return brands.Select(b => new BrandWithModelsDto
        {
            Id = b.Id,
            Name = b.Name,
            Description = b.Description ?? "",
            Models = b.Models.Select(m => new ModelDto
            {
                Id = m.Id,
                Code = m.Code,
                Name = m.Name,
                BrandId = m.BrandId,
                BrandName = b.Name,
                Feature = m.Feature ?? "",
                Verify = m.Verify,
                State = m.State
            }).ToList()
        }).ToList();
    }
}

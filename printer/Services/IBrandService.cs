using printer.Models.Dto;

namespace printer.Services;

/// <summary>
/// 品牌服務介面
/// </summary>
public interface IBrandService
{
    /// <summary>
    /// 取得所有品牌
    /// </summary>
    Task<List<BrandDto>> GetBrandsAsync();

    /// <summary>
    /// 取得型號列表
    /// </summary>
    Task<List<ModelDto>> GetModelsAsync(ModelRequest request);

    /// <summary>
    /// 取得品牌與其型號
    /// </summary>
    Task<List<BrandWithModelsDto>> GetBrandsWithModelsAsync();
}

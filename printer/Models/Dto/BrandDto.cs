namespace printer.Models.Dto;

/// <summary>
/// 品牌資料
/// </summary>
public class BrandDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ModelCount { get; set; }
}

/// <summary>
/// 型號查詢請求
/// </summary>
public class ModelRequest
{
    public int? BrandId { get; set; }
    public string? Keyword { get; set; }
}

/// <summary>
/// 型號資料
/// </summary>
public class ModelDto
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public int BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public string Feature { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Verify { get; set; }
    public string State { get; set; } = string.Empty;
}

/// <summary>
/// 品牌與型號資料
/// </summary>
public class BrandWithModelsDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ModelDto> Models { get; set; } = new();
}

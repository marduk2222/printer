namespace printer.Services;

public class FeatureDefinition
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Controller { get; set; }
}

public interface IPermissionService
{
    List<FeatureDefinition> GetAllFeatures();
    Task<Dictionary<string, bool>> GetRolePermissionsAsync(string role);
    Task SaveRolePermissionsAsync(string role, Dictionary<string, bool> permissions);
    Task<bool> HasPermissionAsync(string role, string featureCode);
    Task InitializeDefaultPermissionsAsync();
}

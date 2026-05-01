using printer.Data.Entities;

namespace printer.Services;

/// <summary>
/// 模組服務介面
/// </summary>
public interface IModuleService
{
    /// <summary>
    /// 取得所有模組
    /// </summary>
    Task<List<SystemModule>> GetAllModulesAsync();

    /// <summary>
    /// 取得已啟用的模組
    /// </summary>
    Task<List<SystemModule>> GetEnabledModulesAsync();

    /// <summary>
    /// 取得單一模組
    /// </summary>
    Task<SystemModule?> GetModuleByIdAsync(int id);

    /// <summary>
    /// 依代碼取得模組
    /// </summary>
    Task<SystemModule?> GetModuleByCodeAsync(string code);

    /// <summary>
    /// 檢查模組是否啟用
    /// </summary>
    Task<bool> IsModuleEnabledAsync(string moduleCode);

    /// <summary>
    /// 切換模組啟用狀態
    /// </summary>
    Task<bool> ToggleModuleAsync(int moduleId);

    /// <summary>
    /// 建立模組
    /// </summary>
    Task<SystemModule> CreateModuleAsync(SystemModule module);

    /// <summary>
    /// 更新模組
    /// </summary>
    Task<SystemModule> UpdateModuleAsync(SystemModule module);

    /// <summary>
    /// 刪除模組
    /// </summary>
    Task<bool> DeleteModuleAsync(int id);

    /// <summary>
    /// 初始化預設模組
    /// </summary>
    Task InitializeDefaultModulesAsync();
}

using Microsoft.AspNetCore.Mvc;
using printer.Services;

namespace printer.ViewComponents;

/// <summary>
/// 模組選單 ViewComponent - 動態生成導覽列中的模組選單
/// </summary>
public class ModuleMenuViewComponent : ViewComponent
{
    private readonly IModuleService _moduleService;

    public ModuleMenuViewComponent(IModuleService moduleService)
    {
        _moduleService = moduleService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var modules = await _moduleService.GetEnabledModulesAsync();
        return View(modules);
    }
}

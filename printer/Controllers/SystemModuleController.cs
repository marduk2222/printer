using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using printer.Data.Entities;
using printer.Services;

namespace printer.Controllers;

/// <summary>
/// 系統模組管理
/// </summary>
[Authorize(Roles = "admin")]
public class SystemModuleController : Controller
{
    private readonly IModuleService _moduleService;

    public SystemModuleController(IModuleService moduleService)
    {
        _moduleService = moduleService;
    }

    public async Task<IActionResult> Index()
    {
        var modules = await _moduleService.GetAllModulesAsync();
        return View(modules);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var module = await _moduleService.GetModuleByIdAsync(id);
        if (module == null) return NotFound();
        return View(module);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(SystemModule module)
    {
        if (!ModelState.IsValid) return View(module);

        await _moduleService.UpdateModuleAsync(module);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(int id)
    {
        await _moduleService.ToggleModuleAsync(id);
        return RedirectToAction(nameof(Index));
    }
}

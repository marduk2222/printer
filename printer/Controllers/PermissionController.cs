using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using printer.Services;

namespace printer.Controllers;

[Authorize(Roles = "admin")]
public class PermissionController : Controller
{
    private readonly IPermissionService _permissionService;

    private static readonly Dictionary<string, string> RoleNames = new()
    {
        ["employee"] = "員工",
        ["supervisor"] = "主管",
        ["admin"] = "系統管理員"
    };

    public PermissionController(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    public async Task<IActionResult> Index()
    {
        var features = _permissionService.GetAllFeatures();
        var roles = new[] { "employee", "supervisor", "admin" };

        var permissions = new Dictionary<string, Dictionary<string, bool>>();
        foreach (var role in roles)
        {
            permissions[role] = await _permissionService.GetRolePermissionsAsync(role);
        }

        ViewBag.Features = features;
        ViewBag.Roles = roles;
        ViewBag.RoleNames = RoleNames;
        ViewBag.Permissions = permissions;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string role, List<string> allowedFeatures)
    {
        var features = _permissionService.GetAllFeatures();
        var permissions = new Dictionary<string, bool>();

        foreach (var f in features)
        {
            permissions[f.Code] = allowedFeatures?.Contains(f.Code) ?? false;
        }

        await _permissionService.SaveRolePermissionsAsync(role, permissions);
        TempData["Success"] = $"已儲存 {RoleNames.GetValueOrDefault(role, role)} 的權限設定";

        return RedirectToAction(nameof(Index));
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using printer.Services;

namespace printer.ViewComponents;

public class UserPermissionViewComponent : ViewComponent
{
    private readonly IPermissionService _permissionService;

    public UserPermissionViewComponent(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var role = HttpContext.User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        var permissions = await _permissionService.GetRolePermissionsAsync(role);
        return View(permissions);
    }
}

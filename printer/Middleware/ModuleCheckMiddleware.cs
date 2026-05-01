using printer.Services;

namespace printer.Middleware;

/// <summary>
/// 模組檢查中介軟體 - 檢查路徑是否需要特定模組啟用
/// </summary>
public class ModuleCheckMiddleware
{
    private readonly RequestDelegate _next;

    // 定義需要模組權限的路徑
    private static readonly Dictionary<string, string> ProtectedPaths = new()
    {
        { "/BillingConfig", "billing" },
        { "/BillingGroup", "billing" },
        { "/BillingTemplate", "billing" },
        { "/Invoice", "billing" },
        { "/BillingReport", "billing" },
        { "/api/v1/billing", "billing" },
        { "/api/v1/invoices", "billing" },
        { "/Einvoice", "einvoice" },
        { "/api/v1/einvoice", "einvoice" },
        { "/WorkOrder", "workorder" },
        { "/api/v1/workorders", "workorder" }
        // /InvoicePrintSettings 不在此保護：billingstyle 停用時仍可存取，但只顯示基本 3 款
    };

    public ModuleCheckMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IModuleService moduleService)
    {
        var path = context.Request.Path.Value ?? "";

        foreach (var (protectedPath, moduleCode) in ProtectedPaths)
        {
            // 精確路徑前綴比對：路徑必須完全等於保護路徑，或以 / 繼續
            // 避免 /Invoice 誤匹配 /InvoicePrintSettings
            var isMatch = path.Equals(protectedPath, StringComparison.OrdinalIgnoreCase)
                       || path.StartsWith(protectedPath + "/", StringComparison.OrdinalIgnoreCase)
                       || path.StartsWith(protectedPath + "?", StringComparison.OrdinalIgnoreCase);

            if (isMatch)
            {
                if (!await moduleService.IsModuleEnabledAsync(moduleCode))
                {
                    // 如果是 API 請求，返回 JSON 錯誤
                    if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Response.StatusCode = 403;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync("{\"error\": \"Module not enabled\", \"module\": \"" + moduleCode + "\"}");
                        return;
                    }

                    // 如果是網頁請求，重導向到首頁
                    context.Response.Redirect("/");
                    return;
                }
                break;
            }
        }

        await _next(context);
    }
}

/// <summary>
/// 擴充方法
/// </summary>
public static class ModuleCheckMiddlewareExtensions
{
    public static IApplicationBuilder UseModuleCheck(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ModuleCheckMiddleware>();
    }
}

using System.Diagnostics;
using System.Text;
using printer.Services;

namespace printer.Middleware;

/// <summary>
/// 攔截 /api/* 的 request / response，寫到 ./Log/Api{yyyyMMdd}.txt。
/// Body 完整記錄（不截斷），方便日後查 SNMP 原始資料。
/// 不影響非 /api/* 路徑（MVC View、static 資源等）。
/// </summary>
public class ApiLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiFileLogger _logger;

    public ApiLoggingMiddleware(RequestDelegate next, ApiFileLogger logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        var startedAt = DateTime.Now;
        var method = context.Request.Method;

        // 讀 request body（要 buffer 才能讓 controller 也讀到）
        context.Request.EnableBuffering();
        var reqBody = await ReadStreamAsync(context.Request.Body);
        context.Request.Body.Position = 0;

        // 替換 response stream 攔截 body，最後再 copy 回原 stream
        var origResp = context.Response.Body;
        using var memStream = new MemoryStream();
        context.Response.Body = memStream;

        Exception? exception = null;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            string respBody = "";
            try
            {
                memStream.Position = 0;
                respBody = await ReadStreamAsync(memStream);
                memStream.Position = 0;
                await memStream.CopyToAsync(origResp);
            }
            catch { /* 即使讀 body 失敗也不影響 log */ }
            finally
            {
                context.Response.Body = origResp;
            }

            try
            {
                _logger.Write(new ApiLogEntry
                {
                    Timestamp    = startedAt,
                    Method       = method,
                    Path         = path + (context.Request.QueryString.HasValue ? context.Request.QueryString.Value : ""),
                    StatusCode   = context.Response.StatusCode,
                    DurationMs   = sw.ElapsedMilliseconds,
                    RemoteIp     = context.Connection.RemoteIpAddress?.ToString() ?? "",
                    RequestBody  = reqBody ?? "",
                    ResponseBody = respBody ?? "",
                    Exception    = exception?.Message,
                });
            }
            catch { /* logging 不可拖垮 request */ }
        }
    }

    private static async Task<string> ReadStreamAsync(Stream stream)
    {
        if (stream == null || !stream.CanRead) return "";
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}

using System.Collections.Concurrent;
using System.Text;

namespace printer.Services;

/// <summary>
/// /api/* 流量 log 結構（一筆 request + response）
/// </summary>
public class ApiLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Method { get; set; } = "";
    public string Path { get; set; } = "";
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string RemoteIp { get; set; } = "";
    public string RequestBody { get; set; } = "";
    public string ResponseBody { get; set; } = "";
    public string? Exception { get; set; }
}

/// <summary>
/// /api/* 流量 log，背景 task 寫到 ./Log/Api{yyyyMMdd}.txt（避免 IO 阻塞 request 線程）。
/// 同時是 singleton 與 IHostedService —— 啟動時開 worker、關閉時 flush queue。
/// </summary>
public sealed class ApiFileLogger : IHostedService, IDisposable
{
    private readonly BlockingCollection<ApiLogEntry> _queue = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _worker;
    private readonly string _logFolder;

    public ApiFileLogger()
    {
        _logFolder = Path.Combine(AppContext.BaseDirectory, "Log");
        Directory.CreateDirectory(_logFolder);
    }

    public void Write(ApiLogEntry entry)
    {
        if (entry == null) return;
        try { _queue.Add(entry); } catch { /* queue 已 Dispose 或 CompleteAdding */ }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _worker = Task.Factory.StartNew(RunLoop, TaskCreationOptions.LongRunning);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.CompleteAdding();
        try { _worker?.Wait(2000); } catch { }
        _cts.Cancel();
        return Task.CompletedTask;
    }

    private void RunLoop()
    {
        try
        {
            foreach (var entry in _queue.GetConsumingEnumerable(_cts.Token))
            {
                try
                {
                    var date = entry.Timestamp.ToString("yyyyMMdd");
                    var path = Path.Combine(_logFolder, $"Api{date}.txt");
                    File.AppendAllText(path, FormatEntry(entry) + Environment.NewLine, Encoding.UTF8);
                }
                catch { /* never let logging crash app */ }
            }
        }
        catch (OperationCanceledException) { }
    }

    private static string FormatEntry(ApiLogEntry e)
    {
        var sb = new StringBuilder();
        sb.Append(e.Timestamp.ToString("HH:mm:ss.fff")).Append(' ');
        sb.Append(e.Method).Append(' ');
        sb.Append(e.Path).Append(' ');
        sb.Append("status=").Append(e.StatusCode).Append(' ');
        sb.Append("dur=").Append(e.DurationMs).Append("ms ");
        sb.Append("from=").Append(e.RemoteIp);
        if (!string.IsNullOrEmpty(e.Exception)) sb.Append(" exception=").Append(e.Exception);
        if (!string.IsNullOrEmpty(e.RequestBody)) sb.Append(Environment.NewLine).Append("  >> ").Append(e.RequestBody);
        if (!string.IsNullOrEmpty(e.ResponseBody)) sb.Append(Environment.NewLine).Append("  << ").Append(e.ResponseBody);
        return sb.ToString();
    }

    public void Dispose()
    {
        try { _queue.Dispose(); } catch { }
        try { _cts.Dispose(); } catch { }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataClass;

namespace printer_setup.Infrastructure
{
    /// <summary>
    /// 取代原本 Thread_Log_Run 的輪詢 Thread。改用 BlockingCollection 背景 Task 消費。
    /// 檔名規則維持 ./Log/{File}{yyyyMMdd}.txt；欄位寬度與表頭格式沿用舊版。
    /// </summary>
    internal sealed class AsyncLogger : IDisposable
    {
        private readonly BlockingCollection<LogInfo> _queue = new BlockingCollection<LogInfo>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Task _worker;
        private readonly string _logFolder;

        public AsyncLogger(string logFolder = @".\Log\")
        {
            _logFolder = logFolder;
            _worker = Task.Factory.StartNew(RunLoop, TaskCreationOptions.LongRunning);
        }

        public void Write(LogInfo info)
        {
            if (info == null) return;
            if (string.IsNullOrEmpty(info.Timestamp))
                info.Timestamp = DateTime.Now.ToString("HH:mm:ss");
            _queue.Add(info);
        }

        private void RunLoop()
        {
            var writers = new Dictionary<string, StreamWriter>();
            var currentDay = DateTime.Now.Day;
            var dateStr = DateTime.Now.ToString("yyyyMMdd");

            try
            {
                foreach (var info in _queue.GetConsumingEnumerable(_cts.Token))
                {
                    try
                    {
                        if (currentDay != DateTime.Now.Day)
                        {
                            FlushAndClose(writers);
                            currentDay = DateTime.Now.Day;
                            dateStr = DateTime.Now.ToString("yyyyMMdd");
                        }

                        var file = info.File ?? "Log";
                        if (!writers.TryGetValue(file, out var sw))
                        {
                            sw = OpenWriter(file, dateStr);
                            writers[file] = sw;
                        }

                        sw.WriteLine(BuildRow(info));
                        sw.Flush();
                    }
                    catch { /* never let logging crash the app */ }
                }
            }
            catch (OperationCanceledException) { }
            finally { FlushAndClose(writers); }
        }

        private StreamWriter OpenWriter(string file, string dateStr)
        {
            if (!Directory.Exists(_logFolder)) Directory.CreateDirectory(_logFolder);
            var path = Path.Combine(_logFolder, $"{file}{dateStr}.txt");
            var exists = File.Exists(path);
            var sw = File.AppendText(path);
            if (!exists) sw.WriteLine(BuildHeader());
            return sw;
        }

        private static string BuildHeader()
        {
            var sb = new StringBuilder();
            sb.Append("#Timestamp".PadRight(20)).Append('\t');
            sb.Append("#Module".PadRight(20)).Append('\t');
            sb.Append("#Category".PadRight(20)).Append('\t');
            sb.Append("#Function".PadRight(20)).Append('\t');
            sb.Append("#Identity".PadRight(20)).Append('\t');
            sb.Append("#Option".PadRight(20)).Append('\t');
            sb.Append("#Message");
            return sb.ToString();
        }

        private static string BuildRow(LogInfo info)
        {
            var sb = new StringBuilder();
            sb.Append((info.Timestamp ?? "").PadRight(20)).Append('\t');
            sb.Append((info.Module ?? "").PadRight(20)).Append('\t');
            sb.Append((info.Category ?? "").PadRight(20)).Append('\t');
            sb.Append((info.Function ?? "").PadRight(20)).Append('\t');
            sb.Append((info.Identity ?? "").PadRight(20)).Append('\t');
            sb.Append((info.Option ?? "").PadRight(20)).Append('\t');
            sb.Append(info.Message ?? "");
            return sb.ToString();
        }

        private static void FlushAndClose(Dictionary<string, StreamWriter> writers)
        {
            foreach (var kvp in writers)
            {
                try { kvp.Value.Flush(); kvp.Value.Dispose(); } catch { }
            }
            writers.Clear();
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
            try { _worker.Wait(2000); } catch { }
            _cts.Cancel();
            _cts.Dispose();
            _queue.Dispose();
        }
    }
}

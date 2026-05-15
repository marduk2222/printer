using System;
using System.Diagnostics;
using System.IO;
using DataClass;
using printer_setup.Infrastructure;

namespace printer_setup.Services
{
    /// <summary>
    /// 把主目錄的 Config.ini 同步到 printer_drv / printer_inf 的子目錄，
    /// 並視需求呼叫 Service.bat 啟動服務（原 ProgramCommand）。
    /// </summary>
    internal class ConfigSync
    {
        private readonly string _baseDirectory;
        private readonly AsyncLogger _logger;

        public ConfigSync(string baseDirectory, AsyncLogger logger)
        {
            _baseDirectory = baseDirectory;
            _logger = logger;
        }

        public void CopyConfigToSubModules()
        {
            try
            {
                var src = Path.Combine(_baseDirectory, "Config.ini");
                if (!File.Exists(src)) return;

                foreach (var sub in new[] { "printer_drv", "printer_inf" })
                {
                    var dir = Path.Combine(_baseDirectory, sub);
                    if (!Directory.Exists(dir)) continue;
                    File.Copy(src, Path.Combine(dir, "Config.ini"), true);
                }
            }
            catch (Exception ex)
            {
                _logger?.Write(new LogInfo { File = "Error", Category = "Build", Option = "copy_config", Message = ex.Message });
            }
        }

        public bool RunCommand(string processName, string arguments = null)
        {
            try
            {
                _logger?.Write(new LogInfo { Module = "ProgramCommand", Option = "Run", Message = processName });
                var proc = new Process();
                proc.StartInfo.FileName = processName;
                proc.StartInfo.Arguments = arguments ?? "";
                return proc.Start();
            }
            catch (Exception ex)
            {
                _logger?.Write(new LogInfo { File = "Error", Module = "ProgramCommand", Message = ex.Message });
                return false;
            }
        }

        /// <summary>
        /// 安裝（或重裝並啟動）printer_info Windows Service。
        /// 流程：sc stop → sc delete → sc create → sc start。
        /// 路徑：{baseDir}/printer_inf/printer_info.exe（與 CopyConfigToSubModules 對應）。
        /// 需要管理員權限執行 printer_setup。
        /// </summary>
        public bool InstallPrinterInfoService()
        {
            const string serviceName = "printer_info";
            var exePath = Path.Combine(_baseDirectory, "printer_inf", "printer_info.exe");
            if (!File.Exists(exePath))
            {
                _logger?.Write(new LogInfo { File = "Error", Module = "Install", Option = "missing_exe",
                    Message = $"找不到 {exePath}（部署時需要 printer_inf 子目錄）" });
                return false;
            }

            _logger?.Write(new LogInfo { Module = "Install", Option = "begin", Message = exePath });

            // 1. 嘗試停止既有服務（不存在就略過，不視為錯誤）
            RunSc("stop "      + serviceName, ignoreFailure: true);
            // 2. 嘗試刪除既有服務（同上）
            RunSc("delete "    + serviceName, ignoreFailure: true);
            // 3. SCM 內部清理稍微等一下
            System.Threading.Thread.Sleep(500);
            // 4. 建立服務（注意 sc 語法：= 後面要有空格）
            var createArgs = $"create {serviceName} binPath= \"{exePath}\" start= auto DisplayName= \"{serviceName}\"";
            if (!RunSc(createArgs))
            {
                _logger?.Write(new LogInfo { File = "Error", Module = "Install", Option = "create_failed", Message = createArgs });
                return false;
            }
            // 5. 設定描述（失敗無傷大雅）
            RunSc($"description {serviceName} \"printer_info 抄表服務\"", ignoreFailure: true);
            // 6. 啟動
            if (!RunSc("start " + serviceName))
            {
                _logger?.Write(new LogInfo { File = "Error", Module = "Install", Option = "start_failed", Message = serviceName });
                return false;
            }

            _logger?.Write(new LogInfo { Module = "Install", Option = "success", Message = serviceName });
            return true;
        }

        private bool RunSc(string arguments, bool ignoreFailure = false)
        {
            try
            {
                var psi = new ProcessStartInfo("sc", arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    if (p == null) return false;
                    var stdout = p.StandardOutput.ReadToEnd();
                    var stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit(15000);
                    var ok = p.ExitCode == 0;
                    _logger?.Write(new LogInfo { Module = "Install", Function = "sc",
                        Option = ok ? "ok" : "fail",
                        Identity = arguments,
                        Message = $"exit={p.ExitCode}; out={stdout.Trim()}; err={stderr.Trim()}" });
                    return ok || ignoreFailure;
                }
            }
            catch (Exception ex)
            {
                _logger?.Write(new LogInfo { File = ignoreFailure ? null : "Error",
                    Module = "Install", Function = "sc", Option = "exception",
                    Identity = arguments, Message = ex.Message });
                return ignoreFailure;
            }
        }
    }
}

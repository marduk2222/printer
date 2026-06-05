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
        /// 透過同層的 install.bat 反安裝並重新安裝 printer_info Windows Service。
        /// bat 內含完整 stop → delete → create → start 流程；exit code 0 視為成功。
        /// 需要管理員權限執行 printer_setup（否則 sc 會 ERROR 5: access denied）。
        /// </summary>
        public bool InstallPrinterInfoService()
        {
            const string batName = "install.bat";
            var batPath = Path.Combine(_baseDirectory, batName);
            if (!File.Exists(batPath))
            {
                _logger?.Write(new LogInfo { File = "Error", Module = "Install", Option = "missing_bat",
                    Message = $"找不到 {batPath}（需與 printer_setup.exe 同層）" });
                return false;
            }

            var exePath = Path.Combine(_baseDirectory, "printer_info.exe");
            if (!File.Exists(exePath))
            {
                _logger?.Write(new LogInfo { File = "Error", Module = "Install", Option = "missing_exe",
                    Message = $"找不到 {exePath}（需與 printer_setup.exe 同層）" });
                return false;
            }

            _logger?.Write(new LogInfo { Module = "Install", Option = "begin", Message = batPath });

            try
            {
                // cmd /c "<batPath>"：路徑含空白時整個指令要再用雙引號包一層
                var psi = new ProcessStartInfo("cmd.exe", $"/c \"\"{batPath}\"\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = _baseDirectory
                };
                using (var p = Process.Start(psi))
                {
                    if (p == null)
                    {
                        _logger?.Write(new LogInfo { File = "Error", Module = "Install", Option = "start_failed", Message = batPath });
                        return false;
                    }
                    var stdout = p.StandardOutput.ReadToEnd();
                    var stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit(60000);
                    var ok = p.ExitCode == 0;
                    _logger?.Write(new LogInfo { Module = "Install", Function = "bat",
                        Option = ok ? "ok" : "fail",
                        Identity = batName,
                        Message = $"exit={p.ExitCode}; out={stdout.Trim()}; err={stderr.Trim()}" });
                    if (ok) _logger?.Write(new LogInfo { Module = "Install", Option = "success", Message = "printer_info" });
                    return ok;
                }
            }
            catch (Exception ex)
            {
                _logger?.Write(new LogInfo { File = "Error", Module = "Install", Option = "exception", Message = ex.Message });
                return false;
            }
        }
    }
}

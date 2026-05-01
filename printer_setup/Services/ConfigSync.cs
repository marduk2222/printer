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
    }
}

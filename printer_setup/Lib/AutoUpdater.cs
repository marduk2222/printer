using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Lib
{
    /// <summary>
    /// Auto updater for Printer_inf service
    /// </summary>
    public class AutoUpdater
    {
        private readonly string _serverUrl;
        private readonly string _installPath;
        private readonly Action<string> _log;
        private readonly HttpClient _client;
        private static bool _isUpdating = false;
        private static readonly object _updateLock = new object();

        public string CurrentVersion
        {
            get
            {
                try
                {
                    return Assembly.GetExecutingAssembly().GetName().Version.ToString();
                }
                catch
                {
                    return "1.0.0.0";
                }
            }
        }

        public AutoUpdater(string serverUrl, Action<string> log = null)
        {
            _serverUrl = serverUrl?.TrimEnd('/') ?? "";
            _installPath = AppDomain.CurrentDomain.BaseDirectory;
            _log = log ?? Console.WriteLine;
            _client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        /// <summary>
        /// Check if update is available
        /// Uses /api/service/version endpoint for Printer_inf service (RESTful GET)
        /// </summary>
        public async Task<UpdateInfo> CheckUpdateAsync()
        {
            if (string.IsNullOrEmpty(_serverUrl))
            {
                _log("[AutoUpdater] No server URL configured");
                return null;
            }

            try
            {
                _log($"[AutoUpdater] Checking update... current version: {CurrentVersion}");

                // Use RESTful GET with query parameter
                var url = $"{_serverUrl}/api/service/version?version={CurrentVersion}";
                var response = await _client.GetAsync(url);
                var result = await response.Content.ReadAsStringAsync();

                var data = JsonConvert.DeserializeObject<UpdateResponse>(result);

                if (data?.status == "success" && data.data != null)
                {
                    _log($"[AutoUpdater] Latest version: {data.data.latest_version}, need_update: {data.data.need_update}");
                    return data.data;
                }
            }
            catch (Exception ex)
            {
                _log($"[AutoUpdater] CheckUpdate failed: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Download and prepare update
        /// </summary>
        public async Task<bool> DownloadAndInstallAsync(UpdateInfo info)
        {
            if (!info.need_update || string.IsNullOrEmpty(info.download_url))
            {
                _log("[AutoUpdater] No update needed or no download URL");
                return false;
            }

            // Prevent concurrent updates
            lock (_updateLock)
            {
                if (_isUpdating)
                {
                    _log("[AutoUpdater] Update already in progress, skipping...");
                    return false;
                }
                _isUpdating = true;
            }

            // Use unique temp path to avoid conflicts with locked files (include milliseconds + random)
            var uniqueId = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            var tempPath = Path.Combine(Path.GetTempPath(), $"Printer_inf_update_{uniqueId}");
            var zipPath = Path.Combine(tempPath, "update.zip");

            _log($"[AutoUpdater] Temp path: {tempPath}");

            try
            {
                // 1. Create temp directory (cleanup old ones first)
                CleanupOldTempFolders();
                Directory.CreateDirectory(tempPath);

                // 2. Download update package
                _log($"[AutoUpdater] Downloading version {info.latest_version}...");
                var bytes = await _client.GetByteArrayAsync(info.download_url);
                File.WriteAllBytes(zipPath, bytes);
                _log($"[AutoUpdater] Downloaded {bytes.Length} bytes");

                // 3. Extract (file by file to handle locked files)
                _log("[AutoUpdater] Extracting...");
                var extractedCount = 0;
                var skippedFiles = new System.Collections.Generic.List<string>();

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue; // Skip directories

                        var destPath = Path.Combine(tempPath, entry.FullName);
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);

                        try
                        {
                            entry.ExtractToFile(destPath, true);
                            extractedCount++;
                        }
                        catch (Exception ex)
                        {
                            _log($"[AutoUpdater] Skip extract {entry.Name}: {ex.Message}");
                            skippedFiles.Add(entry.Name);
                        }
                    }
                }
                _log($"[AutoUpdater] Extracted {extractedCount} files, skipped {skippedFiles.Count}");

                // 4. Start updater and exit service
                _log("[AutoUpdater] Starting updater...");
                var updaterPath = Path.Combine(tempPath, "Updater.exe");

                // If update package doesn't have Updater.exe or extraction failed, use local one
                if (!File.Exists(updaterPath))
                {
                    _log("[AutoUpdater] Using local Updater.exe");
                    updaterPath = Path.Combine(_installPath, "Updater.exe");
                }

                if (!File.Exists(updaterPath))
                {
                    _log("[AutoUpdater] Updater.exe not found!");
                    return false;
                }

                // Normalize paths - remove trailing backslashes to avoid escaping issues with \"
                var installPathArg = Path.GetFullPath(_installPath).TrimEnd('\\');
                var tempPathArg = Path.GetFullPath(tempPath).TrimEnd('\\');

                _log($"[AutoUpdater] Install path: {installPathArg}");
                _log($"[AutoUpdater] Temp path: {tempPathArg}");
                _log($"[AutoUpdater] Updater: {updaterPath}");

                // Build arguments string with proper quoting
                var arguments = $"\"{installPathArg}\" \"{tempPathArg}\" \"Printer_inf\"";
                _log($"[AutoUpdater] Full command: \"{updaterPath}\" {arguments}");

                var psi = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = arguments,
                    UseShellExecute = true,  // Use shell to inherit SYSTEM privileges
                    WorkingDirectory = Path.GetDirectoryName(updaterPath)
                };

                Process.Start(psi);

                return true; // Caller should stop the service
            }
            catch (Exception ex)
            {
                _log($"[AutoUpdater] Install failed: {ex.Message}");
                _log($"[AutoUpdater] Error details: {ex.GetType().Name} at {ex.StackTrace?.Split('\n')[0]}");
                return false;
            }
            finally
            {
                _isUpdating = false;
            }
        }

        /// <summary>
        /// Report client status to server
        /// </summary>
        public async Task<bool> ReportStatusAsync(string machineId, int partnerId = 0, int printerCount = 0)
        {
            if (string.IsNullOrEmpty(_serverUrl) || string.IsNullOrEmpty(machineId))
                return false;

            try
            {
                var payload = JsonConvert.SerializeObject(new
                {
                    machine_id = machineId,
                    version = CurrentVersion,
                    partner_id = partnerId,
                    os_info = Environment.OSVersion.ToString(),
                    printer_count = printerCount
                });

                var content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                var response = await _client.PostAsync($"{_serverUrl}/api/report", content);
                var result = await response.Content.ReadAsStringAsync();

                var data = JsonConvert.DeserializeObject<UpdateResponse>(result);
                return data?.status == "success";
            }
            catch (Exception ex)
            {
                _log($"[AutoUpdater] ReportStatus failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Cleanup old temp folders from previous update attempts
        /// </summary>
        private void CleanupOldTempFolders()
        {
            try
            {
                var tempRoot = Path.GetTempPath();
                foreach (var dir in Directory.GetDirectories(tempRoot, "Printer_inf_update_*"))
                {
                    try
                    {
                        Directory.Delete(dir, true);
                        _log($"[AutoUpdater] Cleaned up: {dir}");
                    }
                    catch
                    {
                        // Ignore locked folders
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Get machine ID (use MAC address or hostname)
        /// </summary>
        public static string GetMachineId()
        {
            try
            {
                // Try to get first network adapter MAC address
                foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                        nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    {
                        var mac = nic.GetPhysicalAddress().ToString();
                        if (!string.IsNullOrEmpty(mac) && mac != "000000000000")
                        {
                            return mac;
                        }
                    }
                }
            }
            catch { }

            // Fallback to hostname
            return Environment.MachineName;
        }
    }

    #region Response Classes

    public class UpdateResponse
    {
        public string status { get; set; }
        public string message { get; set; }
        public UpdateInfo data { get; set; }
    }

    public class UpdateInfo
    {
        public string current_version { get; set; }
        public string latest_version { get; set; }
        public bool need_update { get; set; }
        public bool force_update { get; set; }
        public string download_url { get; set; }
        public string changelog { get; set; }
    }

    #endregion
}

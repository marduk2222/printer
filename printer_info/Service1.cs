using DataClass;
using System;
using System.Collections.Generic;
using System.ServiceProcess;

namespace printer_info
{
    public partial class Service1 : ServiceBase
    {
        #region Parameter
        //========== System ==========
        //private static string Module = "Main";
        private bool IsClose;

        //========== Directory ==========
        private string base_directory;

        //========== Parameter ==========

        //========== Thread Parameter ==========
        private System.Collections.Generic.List<LogInfo> g_List1 = new System.Collections.Generic.List<LogInfo>();                  //Log資料

        //========== Svc Parameter(Svc 資訊) ==========
        private string svc_version => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        private string svc_name = "printer_info";
        //private string drv_name = "printer_drv";

        //========== Customer Parameter(客戶資訊) ==========
        private int partner_id = 0;
        private int _lastPrinterCount = 0;

        //========== REST API Parameter ==========
        private Lib.RestClient _restClient = null;

        //========== Auto Update Parameter ==========
        private Lib.AutoUpdater _autoUpdater = null;
        private System.Threading.Timer _updateTimer = null;
        private System.Threading.Timer _reportTimer = null;
        private const int UPDATE_CHECK_INTERVAL_HOURS = 6;  // Check update every 6 hours
        private const int REPORT_INTERVAL_MINUTES = 30;     // Report status every 30 minutes
        #endregion

        //#region DLL
        //[System.Runtime.InteropServices.UnmanagedFunctionPointerAttribute(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        //public delegate int PrinterAction(string ip, int port, System.Text.StringBuilder str);
        //private void Run_Dll(string dllName, string func, string ip, int port, System.Text.StringBuilder str)
        //{
        //    var dll = new Lib.DllInvoke($@"{base_directory}dll\{dllName}.dll");
        //    try
        //    {
        //        var mfpData = (PrinterAction)dll.Invoke(func, typeof(PrinterAction)); if (mfpData == null) return;
        //        mfpData(ip, port, str);
        //    }
        //    catch (Exception ex) { throw ex; }
        //    finally { dll.Close(); }
        //}
        //#endregion

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Trace(new LogInfo() { Category = "Initialize", Function = "OnStart", Option = $"{svc_version}", Message = $"None" });
            base_directory = AppDomain.CurrentDomain.BaseDirectory;

            partner_id = Lib.Setup.General.Partner;

            // Initialize REST API settings
            var restUrl = Lib.Setup.General.RestUrl;
            var restDb = Lib.Setup.General.RestDb;
            var restLogin = Lib.Setup.General.RestLogin;
            var restPassword = Lib.Setup.General.RestPassword;
            _restClient = new Lib.RestClient(restUrl, Trace, restDb, restLogin, restPassword);
            Trace(new LogInfo() { Category = "Initialize", Function = "OnStart", Option = "REST", Message = $"RestUrl={restUrl}, Db={restDb}, Login={restLogin}" });

            new System.Threading.Thread(Thread_Log_Run).Start(); System.Threading.Thread.Sleep(100);
            new System.Threading.Thread(Thread_Run).Start(); System.Threading.Thread.Sleep(100);
            new System.Threading.Thread(Thread_Printer_Run).Start(); System.Threading.Thread.Sleep(100);

            // Initialize Auto Update
            _autoUpdater = new Lib.AutoUpdater(restUrl, msg => Trace(new LogInfo() { Category = "AutoUpdate", Function = "Log", Message = msg }));

            // Start update check timer (first check after 1 minute, then every 6 hours)
            _updateTimer = new System.Threading.Timer(
                CheckForUpdates,
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromHours(UPDATE_CHECK_INTERVAL_HOURS));

            // Start status report timer (first report after 30 seconds, then every 30 minutes)
            _reportTimer = new System.Threading.Timer(
                ReportClientStatus,
                null,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromMinutes(REPORT_INTERVAL_MINUTES));

            Trace(new LogInfo() { Category = "Initialize", Function = "OnStart", Option = "AutoUpdate", Message = $"Auto update enabled, check interval: {UPDATE_CHECK_INTERVAL_HOURS}h" });
        }
        protected override void OnStop()
        {
            IsClose = true;

            // Dispose timers
            _updateTimer?.Dispose();
            _reportTimer?.Dispose();
        }

        #region Auto Update Methods
        /// <summary>
        /// Check for updates periodically
        /// </summary>
        private async void CheckForUpdates(object state)
        {
            if (_autoUpdater == null) return;

            try
            {
                Trace(new LogInfo() { Category = "AutoUpdate", Function = "CheckForUpdates", Option = "start", Message = "Checking for updates..." });

                var updateInfo = await _autoUpdater.CheckUpdateAsync();

                if (updateInfo?.need_update == true)
                {
                    Trace(new LogInfo() { Category = "AutoUpdate", Function = "CheckForUpdates", Option = "found", Message = $"New version available: {updateInfo.latest_version}" });

                    var success = await _autoUpdater.DownloadAndInstallAsync(updateInfo);
                    if (success)
                    {
                        Trace(new LogInfo() { Category = "AutoUpdate", Function = "CheckForUpdates", Option = "install", Message = "Update downloaded, stopping service for update..." });
                        // Stop the service to allow Updater to replace files
                        Stop();
                    }
                }
                else
                {
                    Trace(new LogInfo() { Category = "AutoUpdate", Function = "CheckForUpdates", Option = "none", Message = "No update available" });
                }
            }
            catch (Exception ex)
            {
                Trace(new LogInfo() { File = "Error", Category = "AutoUpdate", Function = "CheckForUpdates", Option = "exception", Message = ex.Message });
            }
        }

        /// <summary>
        /// Report client status to server periodically
        /// </summary>
        private async void ReportClientStatus(object state)
        {
            if (_autoUpdater == null) return;

            try
            {
                var machineId = Lib.AutoUpdater.GetMachineId();
                var printerCount = _lastPrinterCount;

                var success = await _autoUpdater.ReportStatusAsync(
                    machineId,
                    partner_id,
                    printerCount
                );

                if (success)
                {
                    Trace(new LogInfo() { Category = "AutoUpdate", Function = "ReportClientStatus", Option = "success", Message = $"Status reported: {machineId}" });
                }
            }
            catch (Exception ex)
            {
                Trace(new LogInfo() { File = "Error", Category = "AutoUpdate", Function = "ReportClientStatus", Option = "exception", Message = ex.Message });
            }
        }
        #endregion

        //==================== Trace ====================
        #region Trace
        private void Trace(LogInfo Info)
        {
            try { Info.Timestamp = DateTime.Now.ToString("HH:mm:ss.ffffff"); g_List1.Add(Info); }
            catch { }
        }
        #endregion

        //==================== Thread ====================
        #region Thread
        private void Thread_Log_Run()
        {
            Trace(new LogInfo() { Category = "Thread", Function = "Thread_Log_Run", Option = "Run" });

            //==================== 初始參數 ====================
            //========== 日期 ==========
            var Day = DateTime.Now.Day; var DateStr = DateTime.Now.ToString("yyyyMMdd");
            //========== IOStream暫存 ==========
            var _List = new System.Collections.Generic.List<LogFile>();
            var FolderPath = $@"{base_directory}log\";

            //==================== 執行 ====================
            while (true)
            {
                try
                {
                    //==================== 循環檢查 ====================
                    //========== 檢查暫存筆數 & 休息 ==========
                    if (!IsClose && g_List1.Count == 0) { System.Threading.Thread.Sleep(500); continue; }
                    //========== 檢查今日日期 & 清除IOStream暫存 ==========
                    else if (Day != DateTime.Now.Day)
                    {
                        //===== 清除IOStream暫存 =====
                        foreach (var Item in _List) { Item.Clear(); }
                        _List.Clear();
                        //===== 更新日期 =====
                        Day = DateTime.Now.Day; DateStr = DateTime.Now.ToString("yyyyMMdd");
                    }
                    //========== 檢查今日日期 & 清除IOStream暫存 & 離開系統 ==========
                    else if (IsClose && g_List1.Count == 0)
                    {
                        //===== 清除IOStream暫存 =====
                        foreach (var Item in _List) { Item.Clear(); }
                        _List.Clear();
                        //===== 離開系統 =====
                        break;
                    }

                    //==================== 資料紀錄(Log) ====================
                    //========== 取得資料 & 正規化 ==========
                    var Info = g_List1[0]; g_List1.RemoveAt(0); if (Info == null) continue;
                    var File = (Info.File == null) ? "Log" : Info.File;
                    var Timestamp = (Info.Timestamp == null) ? DateTime.Now.ToString("HH:mm:ss.ffffff") : Info.Timestamp;
                    var Module = (Info.Module == null) ? "" : Info.Module;
                    var Category = (Info.Category == null) ? "" : Info.Category;
                    var Function = (Info.Function == null) ? "" : Info.Function;
                    var Identity = (Info.Identity == null) ? "" : Info.Identity;
                    var Option = (Info.Option == null) ? "" : Info.Option;
                    var Message = (Info.Message == null) ? "" : Info.Message;

                    //========== 檢查IOStream紀錄有無保留(檔案紀錄) ==========
                    var IsExist = false;
                    var Index = (_List == null) ? 0 : _List.Count;
                    for (int i = 0; i < Index; i++) { if (_List[i].File == File) { Index = i; IsExist = true; break; } }

                    //===== 文字暫存 =====
                    var Content = new System.Text.StringBuilder();
                    //========== 存放IOStream紀錄無保留 ==========
                    if (!IsExist)
                    {
                        //===== 刪除超過七天的紀錄 =====
                        if (!System.IO.Directory.Exists(FolderPath)) { System.IO.Directory.CreateDirectory(FolderPath); }
                        else
                        {
                            var files = System.IO.Directory.GetFiles(FolderPath);
                            foreach (var file in files)
                            {
                                var fileinfo = new System.IO.FileInfo(file);
                                if (fileinfo.CreationTime < DateTime.Now.AddDays(-7)) { fileinfo.Delete(); }
                            }
                        }

                        //===== 建立資料夾 =====
                        var FilePath = $"{FolderPath}{File}{DateStr}.txt";
                        if (!System.IO.File.Exists(FilePath))
                        {
                            //===== 建立記錄標題 =====
                            Content.Append("#Timestamp".PadRight(20)); Content.Append('\t', 1);
                            Content.Append("#Module".PadRight(20)); Content.Append('\t', 1);
                            Content.Append("#Category".PadRight(20)); Content.Append('\t', 1);
                            Content.Append("#Function".PadRight(20)); Content.Append('\t', 1);
                            Content.Append("#Identity".PadRight(20)); Content.Append('\t', 1);
                            Content.Append("#Option".PadRight(20)); Content.Append('\t', 1);
                            Content.Append("#Message"); //Content.Append('\t', 1);
                        }
                        var StreamWriter = System.IO.File.AppendText(FilePath);
                        StreamWriter.WriteLine(Content.ToString()); Content.Clear();
                        _List.Add(new LogFile() { File = File, StreamWriter = StreamWriter });
                    }
                    //========== 寫紀錄 ==========
                    Content.Append(Timestamp.PadRight(20)); Content.Append('\t', 1);
                    Content.Append(Module.PadRight(20)); Content.Append('\t', 1);
                    Content.Append(Category.PadRight(20)); Content.Append('\t', 1);
                    Content.Append(Function.PadRight(20)); Content.Append('\t', 1);
                    Content.Append(Identity.PadRight(20)); Content.Append('\t', 1);
                    Content.Append(Option.PadRight(20)); Content.Append('\t', 1);
                    Content.Append(Message); //Content.Append('\t', 2);
                    _List[Index].StreamWriter.WriteLine(Content.ToString()); Content.Clear();
                    _List[Index].StreamWriter.Flush();
                }
                catch { if (g_List1.Count > 128) g_List1.Clear(); }
            }
        }                                                       //Log 寫Log
        private void Thread_Run()
        {
            // 版本檢查已改由 CheckForUpdates Timer（每 6 小時）統一處理
            // 此 Thread 保留以維持相容性，但不再重複執行版本檢查邏輯
            while (!IsClose)
            {
                try { System.Threading.Thread.Sleep(60000); }
                catch { }
            }
            //var timer = 36000;
            //while (!IsClose)
            //{
            //    try
            //    {
            //        if (timer < 36000) { timer++; continue; } else timer = 0;
            //        Trace(new LogInfo() { Category = "Version", Function = "Thread_Run", Option = "checking", Message = $"=== 開始檢查更新 (本機版本: {svc_version}) ===" });
            //        var _svc_version = server_get_svc_version(svc_name);
            //        if (_svc_version == null)
            //        {
            //            Trace(new LogInfo() { Category = "Version", Function = "Thread_Run", Option = "error", Message = "無法取得伺服器版本資訊" });
            //            continue;
            //        }
            //        if (_svc_version != svc_version)
            //        {
            //            Trace(new LogInfo() { Category = "Version", Function = "Thread_Run", Option = "updating", Message = $"★★★ 開始執行更新程序 ★★★" });
            //            svc_set_update();
            //        }
            //    }
            //    catch (Exception ex) { Trace(new LogInfo() { File = "Error", Category = "Thread", Function = "Thread_Run", Option = "exception", Message = ex.Message }); }
            //    finally { System.Threading.Thread.Sleep(1000); }
            //}
        }
        private void Thread_Printer_Run()
        {
            var timer = 3590;
            //var c_period = new C_Period() { dealer_id = dealer_id, company_id = company_id, place_id = place_id };
            while (!IsClose)
            {
                try
                {
                    //========== Install ==========

                    //========== Timer ==========
                    //每小時執行下列動作
                    if (timer < 3600) { timer++; continue; } else timer = 0;
                    Printer_Run();
                }
                catch (Exception ex) { Trace(new LogInfo() { File = "Error", Category = "Thread", Function = "Thread_Printer_Run", Option = "exception", Message = ex.Message }); }
                finally { System.Threading.Thread.Sleep(1000); }
            }
        }
        #endregion

        //==================== Control ====================
        #region Control

        //========== Server <-> Svc ==========
        #region Server
        //===== Svc Server ====
        //取得Svc狀態 (RESTful GET with session)
        private string server_get_svc_version(string svc_name)
        {
            try
            {
                if (_restClient == null) return null;

                var versionInfo = _restClient.GetServiceVersion(svc_version);
                if (versionInfo != null && versionInfo.status == "success" && versionInfo.data != null)
                {
                    // Only update if server says need_update=true
                    if (versionInfo.data.need_update)
                    {
                        _updateDownloadUrl = versionInfo.data.download_url;
                        Trace(new LogInfo() { Category = "Server", Function = "server_get_svc_version", Option = "UPDATE_NEEDED", Message = $"★★★ 需要更新! 本機={svc_version}, 伺服器={versionInfo.data.latest_version}, 下載網址={_updateDownloadUrl}" });
                        return versionInfo.data.latest_version;  // 回傳新版本號，觸發更新
                    }
                    else
                    {
                        Trace(new LogInfo() { Category = "Server", Function = "server_get_svc_version", Option = "NO_UPDATE", Message = $"版本已是最新: 本機={svc_version}, 伺服器={versionInfo.data.latest_version}" });
                        return svc_version;  // 不需更新，回傳本機版本（不觸發更新）
                    }
                }
                return null;
            }
            catch (Exception ex) { Trace(new LogInfo() { File = "Error", Category = "Server", Function = "server_get_svc_version", Option = "exception", Message = $"{ex.Message}" }); }
            finally { System.Threading.Thread.Sleep(100); }
            return null;
        }

        // Store download URL from version check
        private string _updateDownloadUrl = null;
        //取得檔案
        //private bool server_get_dll(FileInfo _file)
        //{
        //    try
        //    {
        //        //===== Svc Server =====
        //        var file = Newtonsoft.Json.JsonConvert.SerializeObject(_file);
        //        var json = Newtonsoft.Json.JsonConvert.SerializeObject(new Transfer() { company_id = company_id, place_id = place_id, action = "dll", result = file });
        //        var bytes = Socket_File(url_to_ip(server_url), server_port, json, _file); //取得檔案
        //        if (bytes == null) return false;

        //        create_dll(_file, bytes);

        //        Trace(new LogInfo() { Module = "Socket", Category = "Server", Function = "server_get_dll", Message = $"dll: {_file.file_name}" });
        //    }
        //    catch (Exception ex) { Trace(new LogInfo() { File = "Error", Module = "Socket", Category = "Server", Function = "server_get_dll", Message = $"{ex.Message}" }); }
        //    finally { System.Threading.Thread.Sleep(100); }
        //    return true;
        //}
        #endregion

        //========== Svc <-> Svc ==========
        #region Svc
        private bool svc_set_update()
        {
            try
            {
                if (string.IsNullOrEmpty(_updateDownloadUrl)) return false;

                Trace(new LogInfo() { Category = "Svc", Function = "svc_set_update", Option = "START", Message = $"★ 開始下載更新檔案: {_updateDownloadUrl}" });

                // Use AutoUpdater to download and install
                var updateInfo = new Lib.UpdateInfo
                {
                    need_update = true,
                    latest_version = "new",
                    download_url = _updateDownloadUrl
                };

                var restUrl = Lib.Setup.General.RestUrl;
                var updater = new Lib.AutoUpdater(restUrl, msg => Trace(new LogInfo() { Category = "AutoUpdate", Function = "svc_set_update", Message = msg }));

                var success = updater.DownloadAndInstallAsync(updateInfo).Result;
                if (success)
                {
                    Trace(new LogInfo() { Category = "Svc", Function = "svc_set_update", Option = "SUCCESS", Message = "★★★ 更新下載完成，正在停止服務並啟動更新程式... ★★★" });
                    Stop(); // Stop service to allow Updater to replace files
                }
                else
                {
                    Trace(new LogInfo() { Category = "Svc", Function = "svc_set_update", Option = "FAILED", Message = "✗ 更新下載或安裝失敗" });
                }
                return success;
            }
            catch (Exception ex) { Trace(new LogInfo() { File = "Error", Category = "Svc", Function = "svc_set_update", Option = "exception", Message = $"{ex.Message}" }); }
            finally { System.Threading.Thread.Sleep(100); }
            return true;
        }
        //private bool svc_set_delete()
        //{
        //    try
        //    {
        //        //===== Svc Server =====
        //        var json = Newtonsoft.Json.JsonConvert.SerializeObject(new Transfer() { company_id = company_id, place_id = place_id, action = "delete", result = "CPAIPCAsstSvc" });
        //        var value = SvcSocket(msv_port, json);        //傳送「刪除動作」
        //    }
        //    catch (Exception ex) { Trace(new LogInfo() { File = "Error", Module = "Socket", Category = "Delete", Function = "svc_set_delete", Message = $"{ex.Message}" }); }
        //    finally { System.Threading.Thread.Sleep(100); }
        //    return true;
        //}

        //開關Svc
        //private bool SvcSwitch(string _SvcName, bool _Switch)
        //{
        //    var result = string.Empty;
        //    try
        //    {
        //        var Services = ServiceController.GetServices();
        //        foreach (var service in Services)
        //        {
        //            if (service.ServiceName.ToLower() != _SvcName.ToLower()) continue;
        //            if (_Switch && service.Status == ServiceControllerStatus.Stopped) { result = "Start"; service.Start(); }        //啟動 
        //            else if (!_Switch && service.Status == ServiceControllerStatus.Running) { result = "Stop"; service.Stop(); }    //關閉
        //            else if (_Switch && service.Status == ServiceControllerStatus.Running) { result = "Running"; }                  //運行中
        //            else if (!_Switch && service.Status == ServiceControllerStatus.Stopped) { result = "Stopped"; }                 //已停止
        //            else return true;
        //        }
        //        return true;
        //    }
        //    catch (Exception ex) { Trace(new LogInfo() { File = "Error", Category = "Svc", Function = "SvcSwitch", Option = "exception", Message = $"{ex.Message}" }); }
        //    finally { Trace(new LogInfo() { Category = "Svc", Function = "SvcSwitch", Option = "Finally", Message = $"{_SvcName}:{_Switch}/{result}" }); }
        //    return false;
        //}
        ////刪除Svc
        //private void SvcDelete(string svc_name)
        //{
        //    try
        //    {
        //        do { System.Threading.Thread.Sleep(5000); }
        //        while (!SvcSwitch(svc_name, false));

        //        new ServiceInstaller() { ServiceName = svc_name }.Uninstall(null);
        //        new System.IO.DirectoryInfo(svc_name.Replace("CPAI", "")).Delete(true);
        //    }
        //    catch (Exception ex) { Trace(new LogInfo() { File = "Error", Module = "Svc <-> Svc", Category = "Svc", Function = "SvcDelete", Message = $"{ex.Message}" }); }
        //    finally { Trace(new LogInfo() { Module = "Svc <-> Svc", Category = "Svc", Function = "SvcDelete", Message = $"{svc_name}:Delete" }); }
        //}
        //更新Svc
        //private void SvcUpdate(SvcInfo _svc)
        //{
        //    var time = 30;
        //    try
        //    {
        //        //===== Svc =====
        //        do { System.Threading.Thread.Sleep(5000); }
        //        while (!SvcSwitch(_svc.svc_name, false));
        //        Lib.Setup.Svc.Install = _svc.svc_name;
        //        var timer = 0;
        //        while (!IsClose)
        //        {
        //            try
        //            {
        //                //========== IP Address ==========
        //                if (timer < time) { timer++; continue; }
        //                timer = 0;

        //                var dir = server_get_files_list(_svc);
        //                if (dir == null) continue;

        //                //未連線至Manager Server則繼續嘗試連線
        //                if (dir_file(_svc, dir)) break;
        //            }
        //            catch (Exception ex) { Trace(new LogInfo() { File = "Error", Module = "Svc <-> Svc", Category = "Socket", Function = "ServerSocket", Message = $"{ex.Message}" }); }
        //            finally { System.Threading.Thread.Sleep(1000); }
        //        }

        //        //===== 重新執行 =====
        //        Lib.Setup.Svc.Install = "";
        //        svc_control(_svc);
        //    }
        //    catch (Exception ex) { Trace(new LogInfo() { File = "Error", Module = "Svc <-> Svc", Category = "Svc", Function = "SvcUpdate", Message = $"{ex.Message}" }); }
        //    finally { Trace(new LogInfo() { Module = "Svc <-> Svc", Category = "Svc", Function = "SvcUpdate", Message = $"{_svc.svc_name}:Update" }); }
        //}

        //Svc 控制&動作流程
        //private void svc_control_and_action()
        //{
        //    try
        //    {
        //        //========== Svc ==========
        //        svc_control(drv_name);
        //    }
        //    catch (Exception ex) { Trace(new LogInfo() { File = "Error", Category = "Svc", Function = "svc_control_and_action", Option = "exception", Message = $"{ex.Message}" }); }
        //    finally { Trace(new LogInfo() { Category = "Svc", Function = "svc_control_and_action", Option = "Finally" }); }
        //}
        //Svc 控制&動作流程(詳細)
        //private void svc_control(string svc_name)
        //{
        //    try { SvcSwitch(svc_name, true); }
        //    catch (Exception ex) { Trace(new LogInfo() { File = "Error", Category = "Svc", Function = "svc_control", Option = "exception", Message = $"{ex.Message}" }); }
        //    finally { Trace(new LogInfo() { Category = "Svc", Function = "svc_control", Option = "Finally", Message = $"Svc: {svc_name}" }); }
        //}
        #endregion

        #endregion

        #region Printer_Run
        private bool Printer_Run()
        {
            try
            {
                if (_restClient == null) return false;

                //========== Get Printer Period List ==========
                Trace(new LogInfo() { Category = "Run", Function = "Run", Option = "request", Identity = "printer_period", Message = "REST" });

                var list = _restClient.GetPeriod(partner_id);
                Trace(new LogInfo() { Category = "Run", Function = "Run", Option = "response", Identity = "printer_period", Message = $"{list?.Count}" });

                if (list == null || list.Count == 0) return false;
                _lastPrinterCount = list.Count;

                // 整批共用一份 host 識別（避免每筆每種上傳重複呼叫 NetworkInterface 列舉，與 printer_setup 對齊）
                var hostName = Lib.AutoUpdater.GetHostName();
                var hostIp = Lib.AutoUpdater.GetHostIp();

                // ==================== 連線事務機 & 回傳Server ====================
                foreach (var item in list)
                {
                    Trace(new LogInfo() { Category = "Run", Function = "Printer", Option = "connect", Identity = "printer", Message = $"code:{item?.code}/ip:{item?.ip}/brand:{item?.brand}/model:{item?.model}" });

                    // ========== 連線事務機（SNMP 抓取）==========
                    if (Printer(item, hostName, hostIp)) continue;        // 若 SNMP 失敗則跳過此台

                    // ========== 回傳Server ==========
                    SendSupplies(item, hostName, hostIp);                 // 碳粉/感光鼓
                    SendAlerts(item, hostName, hostIp);                   // 告警

                    // 抄表：只有 printer_counter=true 的機器才需送計數
                    //if (item.printer_counter)
                    SendCounter(item, hostName, hostIp);
                }
                Trace(new LogInfo() { Category = "printer_upload", Message = $"done count:{list?.Count}" });

                return true;
            }
            catch (Exception ex) { Trace(new LogInfo() { Category = "Run", Function = "Run", Option = "exception", Message = ex.Message }); }
            finally { System.Threading.Thread.Sleep(1000); }
            return false;
        }
        #endregion

        #region REST API Methods
        /// <summary>
        /// 回傳碳粉/感光鼓資訊給 Server
        /// </summary>
        private void SendSupplies(Printer item, string hostName, string hostIp)
        {
            if (_restClient == null) return;
            try
            {
                var restRequest = new DataClass.SuppliesRequest
                {
                    code = item.code,
                    host_name = hostName,
                    host_ip = hostIp,
                    items = item.supply_items?.Count > 0 ? item.supply_items : null,
                };
                var success = _restClient.UpdateSupplies(restRequest);
                Trace(new LogInfo() { Category = "Run", Function = "SendSupplies", Option = success ? "success" : "failed", Identity = item.code, Message = $"supply_items:{item.supply_items?.Count ?? 0}" });
            }
            catch (Exception ex)
            {
                Trace(new LogInfo() { File = "Error", Category = "Run", Function = "SendSupplies", Option = "exception", Identity = item.code, Message = ex.Message });
            }
        }

        /// <summary>
        /// 回傳告警資訊給 Server
        /// </summary>
        private void SendAlerts(Printer item, string hostName, string hostIp)
        {
            if (_restClient == null) return;
            try
            {
                var restRequest = new DataClass.AlertsRequest
                {
                    code = item.code,
                    host_name = hostName,
                    host_ip = hostIp,
                    alerts = item.alerts,
                };
                var success = _restClient.UpdateAlerts(restRequest);
                Trace(new LogInfo() { Category = "Run", Function = "SendAlerts", Option = success ? "success" : "failed", Identity = item.code, Message = $"alerts:{restRequest.alerts?.Count ?? 0}" });
            }
            catch (Exception ex)
            {
                Trace(new LogInfo() { File = "Error", Category = "Run", Function = "SendAlerts", Option = "exception", Identity = item.code, Message = ex.Message });
            }
        }

        /// <summary>
        /// 回傳抄表計數給 Server（含 output/category/color/size 四維的 items list）
        /// </summary>
        private void SendCounter(Printer item, string hostName, string hostIp)
        {
            if (_restClient == null) return;
            try
            {
                if (item.items == null || item.items.Count == 0)
                {
                    Trace(new LogInfo() { Category = "Run", Function = "SendCounter", Option = "skip", Identity = item.code, Message = "無計數資料 (model_id 不支援或 SNMP 未回傳)" });
                    return;
                }

                var restRequest = new DataClass.RecordRequest
                {
                    code = item.code,
                    date = DateTime.Now.ToString("yyyy-MM-dd"),
                    state = item.state,
                    host_name = hostName,
                    host_ip = hostIp,
                    data = item.snmp_data,
                    items = item.items,
                };

                var success = _restClient.WriteMeter(restRequest);
                Trace(new LogInfo() { Category = "Run", Function = "SendCounter", Option = success ? "success" : "failed", Identity = item.code, Message = $"state:{restRequest.state}/items:{restRequest.items.Count}/date:{restRequest.date}" });
            }
            catch (Exception ex)
            {
                Trace(new LogInfo() { File = "Error", Category = "Run", Function = "SendCounter", Option = "exception", Identity = item.code, Message = ex.Message });
            }
        }
        #endregion

        #region Printer
        private bool Printer(Printer print, string hostName, string hostIp)
        {
            try
            {
                var mac = print.mac;
                var ip = print.ip;
                if (string.IsNullOrEmpty(ip)) return true;
                var brand = print.brand;
                var model = print.model;
                //var printer_counter = mfp.printer_counter;
                Trace(new LogInfo() { Category = "SNMP", Function = "Scan", Option = "ready", Message = $"ip:{ip}/brand:{brand}/model:{model}" });

                var _mac = string.Empty;
                var macs = Printer_MAC(ip);
                foreach (var obj in macs) { if (_mac == string.Empty) _mac = obj.Value.ToString(); }
                var print_names = Printer_PrinterName(ip);
                var _printer_name = string.Empty;
                foreach (var obj in print_names) { if (_printer_name == string.Empty) _printer_name = obj.Value.ToString(); }
                var serial_names = Printer_SerialNumber(ip);
                var _serial_number = string.Empty;
                foreach (var obj in serial_names) { if (_serial_number == string.Empty) _serial_number = obj.Value.ToString(); }

                // 驗證 SNMP 抓到的 mac/serial 是否與 server 紀錄一致；任一識別欄位不符就跳過此台，避免抄表寫到錯誤的事務機
                bool macMismatch = !string.IsNullOrEmpty(mac) && !string.IsNullOrEmpty(_mac) &&
                                      !string.Equals(mac.Trim(), _mac.Trim(), StringComparison.OrdinalIgnoreCase);
                bool serialMismatch = !string.IsNullOrEmpty(print.serial_number) && !string.IsNullOrEmpty(_serial_number) &&
                                      !string.Equals(print.serial_number.Trim(), _serial_number.Trim(), StringComparison.OrdinalIgnoreCase);
                if (macMismatch || serialMismatch)
                {
                    Trace(new LogInfo()
                    {
                        File = "Error",
                        Category = "SNMP",
                        Function = "Scan",
                        Option = "mismatch",
                        Identity = print.code,
                        Message = $"server.mac={mac}/snmp.mac={_mac}/server.serial={print.serial_number}/snmp.serial={_serial_number}"
                    });
                    return true;
                }

                // server 已有任一識別欄位，但 SNMP 連 mac 與 serial 都讀不到 → 無法驗證設備身分，視為異常並跳過
                bool serverHasAnyId = !string.IsNullOrEmpty(mac) || !string.IsNullOrEmpty(print.serial_number);
                bool snmpGotAnyId = !string.IsNullOrEmpty(_mac) || !string.IsNullOrEmpty(_serial_number);
                if (serverHasAnyId && !snmpGotAnyId)
                {
                    Trace(new LogInfo()
                    {
                        File = "Error",
                        Category = "SNMP",
                        Function = "Scan",
                        Option = "no_identity",
                        Identity = print.code,
                        Message = $"server.mac={mac}/server.serial={print.serial_number}/snmp 未讀到任何識別欄位"
                    });
                    return true;
                }

                if (mac == string.Empty || print.printer_name == string.Empty || print.serial_number == string.Empty)
                {
                    print.mac = _mac;
                    print.printer_name = _printer_name;
                    print.serial_number = _serial_number;

                    // Use REST API to update device
                    if (_restClient != null && !string.IsNullOrEmpty(print.code))
                    {
                        var success = _restClient.UpdateDevice(
                            code: print.code,
                            mac: print.mac,
                            ip: print.ip,
                            serialNumber: print.serial_number,
                            printerName: print.printer_name,
                            state: 1,
                            hostName: hostName,
                            hostIp: hostIp
                        );
                        Trace(new LogInfo() { Category = "REST", Function = "Scan", Option = "UpdateDevice", Message = $"code={print.code}, success={success}" });
                    }
                }

                // ── 依 brand / model 呼叫對應的 Counter + Supplies 方法 ─────────
                // brand：廠牌（common/ricoh/ricoh_imc/xerox/toshiba/kyocera）
                // model：具體型號（toshiba 內細分 2555c / 3515ac 等）
                // 向後相容：brand 為空時，退回讀 mfp.model 當廠牌
                var brandKey = (!string.IsNullOrEmpty(print.brand) ? print.brand : print.model)?.ToLowerInvariant();
                var modelKey = (print.model ?? string.Empty).ToLowerInvariant();
                switch (brandKey)
                {
                    case "common": { Counter_Common(print); } break;
                    case "ricoh": { Counter_Ricoh(print); } break;
                    case "ricoh_imc": { Counter_RicohIMC(print); } break;
                    case "xerox": { Counter_Xerox(print); } break;
                    case "toshiba":
                        switch (modelKey)
                        {
                            case "2555c": { Counter_Toshiba_2555C(print); } break;
                            case "3515ac": { Counter_Toshiba_3515AC(print); } break;
                            default: { Counter_Toshiba(print); } break;   // 通用版（未指定具體型號）
                        }
                        break;
                    case "kyocera": { Counter_Kyocera(print); } break;
                    default: break;
                }

                // prtAlertTable OID: 1.3.6.1.2.1.43.18.1.1.COL.hrDevIdx.alertIdx
                //   .7 = prtAlertCode  .8 = prtAlertDescription  .9 = prtAlertTime
                // GetBulk 回傳完整 OID，需用 StartsWith 比對後以 row key（hrDevIdx.alertIdx）對齊
                var alerts = Printer_Alert(ip);
                var alertMap = new System.Collections.Generic.Dictionary<string, Alert>();
                foreach (var obj in alerts)
                {
                    var oid = obj.Key.ToString();
                    var val = obj.Value.ToString();
                    const string pfx7 = "1.3.6.1.2.1.43.18.1.1.7.";
                    const string pfx8 = "1.3.6.1.2.1.43.18.1.1.8.";
                    const string pfx9 = "1.3.6.1.2.1.43.18.1.1.9.";
                    string rowKey = null;
                    if (oid.StartsWith(pfx7)) rowKey = oid.Substring(pfx7.Length);
                    else if (oid.StartsWith(pfx8)) rowKey = oid.Substring(pfx8.Length);
                    else if (oid.StartsWith(pfx9)) rowKey = oid.Substring(pfx9.Length);
                    if (rowKey == null) continue;
                    if (!alertMap.ContainsKey(rowKey)) alertMap[rowKey] = new Alert();
                    if (oid.StartsWith(pfx7)) alertMap[rowKey].code = val;
                    else if (oid.StartsWith(pfx8)) alertMap[rowKey].description = val;
                    else if (oid.StartsWith(pfx9)) alertMap[rowKey].time = val;
                }
                foreach (var kvp in alertMap) print.alerts.Add(kvp.Value);

                // 把套 cat map 後 mfp.items 累加結果整理成「output/category/color/size=sheets」一行寫到 SNMP log
                // 跟 REST WriteMeter items_summary 格式一致，方便日後 grep / 對照
                if (print.items.Count > 0)
                {
                    var parts = new System.Collections.Generic.List<string>(print.items.Count);
                    foreach (var it in print.items)
                        parts.Add($"{it.output}/{it.category}/{it.color}/{it.size}={it.sheets}");
                    Trace(new LogInfo()
                    {
                        Category = "SNMP",
                        Function = "Scan",
                        Option = "items",
                        Identity = print.code,
                        Message = $"total={print.items.Count} | {string.Join(" | ", parts)}"
                    });
                }
                return false;
            }
            catch (Exception ex) { Trace(new LogInfo() { File = "Error", Category = "SNMP", Function = "Scan", Option = "exception", Message = ex.Message }); return true; }
            finally { System.Threading.Thread.Sleep(1000); }
        }

        #region Counter

        /// <summary>
        /// Common（標準 prtMarkerLifeCount）：僅回傳總計數，無法區分 category / color。
        /// OID: 1.3.6.1.2.1.43.10.2.1.4.1.1
        /// </summary>
        private bool Counter_Common(Printer mfp)
        {
            var ip = mfp.ip;
            Trace(new LogInfo() { Category = "SNMP", Function = "Counter_Common", Option = "request", Identity = ip });
            var counter = COMMON_MIBs(ip);
            Trace(new LogInfo() { Category = "SNMP", Function = "Counter_Common", Option = "response", Identity = ip, Message = $"{Newtonsoft.Json.JsonConvert.SerializeObject(counter)}" });
            if (counter == null) return true;

            int totalBlack = 0;
            foreach (var obj in counter)
                if (obj.Key.ToString() == "1.3.6.1.2.1.43.10.2.1.4.1.1")
                    int.TryParse(obj.Value.ToString(), out totalBlack);

            mfp.snmp_data = Newtonsoft.Json.JsonConvert.SerializeObject(counter);
            if (totalBlack > 0)
                mfp.items.Add(new DataClass.CounterItem { output = "printed", category = "print", color = "black", size = "normal", sheets = totalBlack });

            return Supplies(mfp);
        }

        /// <summary>
        /// Ricoh 標準系列（C2800 / C3300 / C3500 / C3505 / C4500 / C5505 等）
        /// OID root: 1.3.6.1.4.1.367.3.2.1.2.19.5.1.9
        ///   .3  Copy:Black    .4  Copy:Duotone    .5  Copy:Color
        ///   .7  FAX:Black
        ///   .9  Print:Black   .10 Print:Duotone   .11 Print:Color
        /// </summary>
        private bool Counter_Ricoh(Printer mfp)
        {
            var ip = mfp.ip;
            Trace(new LogInfo() { Category = "SNMP", Function = "Counter_Ricoh", Option = "request", Identity = ip });
            var counter = RICOH_MIBs(ip);
            Trace(new LogInfo() { Category = "SNMP", Function = "Counter_Ricoh", Option = "response", Identity = ip, Message = $"{Newtonsoft.Json.JsonConvert.SerializeObject(counter)}" });
            if (counter == null) return true;

            int copyBlack = 0, copyDuotone = 0, copyColor = 0;
            int faxBlack = 0;
            int printBlack = 0, printDuotone = 0, printColor = 0;

            foreach (var obj in counter)
                switch (obj.Key.ToString())
                {
                    case "1.3.6.1.4.1.367.3.2.1.2.19.5.1.9.3": int.TryParse(obj.Value.ToString(), out copyBlack); break;
                    case "1.3.6.1.4.1.367.3.2.1.2.19.5.1.9.4": int.TryParse(obj.Value.ToString(), out copyDuotone); break;
                    case "1.3.6.1.4.1.367.3.2.1.2.19.5.1.9.5": int.TryParse(obj.Value.ToString(), out copyColor); break;
                    case "1.3.6.1.4.1.367.3.2.1.2.19.5.1.9.7": int.TryParse(obj.Value.ToString(), out faxBlack); break;
                    case "1.3.6.1.4.1.367.3.2.1.2.19.5.1.9.9": int.TryParse(obj.Value.ToString(), out printBlack); break;
                    case "1.3.6.1.4.1.367.3.2.1.2.19.5.1.9.10": int.TryParse(obj.Value.ToString(), out printDuotone); break;
                    case "1.3.6.1.4.1.367.3.2.1.2.19.5.1.9.11": int.TryParse(obj.Value.ToString(), out printColor); break;
                    default: break;
                }
            mfp.snmp_data = Newtonsoft.Json.JsonConvert.SerializeObject(counter);

            if (copyBlack > 0) mfp.items.Add(new DataClass.CounterItem { output = "printed", category = "copy", color = "black", size = "normal", sheets = copyBlack });
            if (copyDuotone > 0) mfp.items.Add(new DataClass.CounterItem { output = "printed", category = "copy", color = "duotone", size = "normal", sheets = copyDuotone });
            if (copyColor > 0) mfp.items.Add(new DataClass.CounterItem { output = "printed", category = "copy", color = "color_full", size = "normal", sheets = copyColor });
            if (faxBlack > 0) mfp.items.Add(new DataClass.CounterItem { output = "printed", category = "fax", color = "black", size = "normal", sheets = faxBlack });
            if (printBlack > 0) mfp.items.Add(new DataClass.CounterItem { output = "printed", category = "print", color = "black", size = "normal", sheets = printBlack });
            if (printDuotone > 0) mfp.items.Add(new DataClass.CounterItem { output = "printed", category = "print", color = "duotone", size = "normal", sheets = printDuotone });
            if (printColor > 0) mfp.items.Add(new DataClass.CounterItem { output = "printed", category = "print", color = "color_full", size = "normal", sheets = printColor });

            return Supplies(mfp);
        }

        /// <summary>
        /// Ricoh IMC 系列（IM C系列彩色機）：OID 結構與 Ricoh 相同。
        /// </summary>
        private bool Counter_RicohIMC(Printer mfp) => Counter_Ricoh(mfp);

        /// <summary>
        /// Xerox（xcmHrDevDetailValueString）
        /// OID root: 1.3.6.1.4.1.253.8.53.13.2.1.6.1.20
        ///   .7   Black Printed Impressions（黑白 總列印）
        ///   .10  Black Printed Large Sheets（黑白 大張）
        ///   .29  Color Printed Impressions（彩色 總列印）
        ///   .32  Color Printed Large Sheets（彩色 大張）
        ///   .71  Fax Impressions（傳真）
        /// </summary>
        private bool Counter_Xerox(Printer mfp)
        {
            var ip = mfp.ip;
            Trace(new LogInfo() { Category = "SNMP", Function = "Counter_Xerox", Option = "request", Identity = ip });
            var counter = XEROX_MIBs(ip);
            Trace(new LogInfo() { Category = "SNMP", Function = "Counter_Xerox", Option = "response", Identity = ip, Message = $"{Newtonsoft.Json.JsonConvert.SerializeObject(counter)}" });
            if (counter == null) return true;

            int printBlack = 0, printBlackLarge = 0;
            int printColor = 0, printColorLarge = 0;
            int faxBlack = 0;

            foreach (var obj in counter)
                switch (obj.Key.ToString())
                {
                    case "1.3.6.1.4.1.253.8.53.13.2.1.6.1.20.7": int.TryParse(obj.Value.ToString(), out printBlack); break;
                    case "1.3.6.1.4.1.253.8.53.13.2.1.6.1.20.10": int.TryParse(obj.Value.ToString(), out printBlackLarge); break;
                    case "1.3.6.1.4.1.253.8.53.13.2.1.6.1.20.29": int.TryParse(obj.Value.ToString(), out printColor); break;
                    case "1.3.6.1.4.1.253.8.53.13.2.1.6.1.20.32": int.TryParse(obj.Value.ToString(), out printColorLarge); break;
                    case "1.3.6.1.4.1.253.8.53.13.2.1.6.1.20.71": int.TryParse(obj.Value.ToString(), out faxBlack); break;
                    default: break;
                }
            mfp.snmp_data = Newtonsoft.Json.JsonConvert.SerializeObject(counter);

            if (printBlack > 0) mfp.items.Add(new DataClass.CounterItem { output = "printed", category = "print", color = "black", size = "normal", sheets = printBlack });
            if (printBlackLarge > 0) mfp.items.Add(new DataClass.CounterItem { output = "printed", category = "print", color = "black", size = "large", sheets = printBlackLarge });
            if (printColor > 0) mfp.items.Add(new DataClass.CounterItem { output = "printed", category = "print", color = "color_full", size = "normal", sheets = printColor });
            if (printColorLarge > 0) mfp.items.Add(new DataClass.CounterItem { output = "printed", category = "print", color = "color_full", size = "large", sheets = printColorLarge });
            if (faxBlack > 0) mfp.items.Add(new DataClass.CounterItem { output = "printed", category = "fax", color = "black", size = "normal", sheets = faxBlack });

            return Supplies(mfp);
        }

        // Toshiba OID 結構：1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.{cat}.1.{color}
        //   color: .1=全彩 .2=雙色 .3=黑白
        //   cat: 不同型號用不同 cat 集（見三個對應表）
        private sealed class ToshibaCat
        {
            public string Output;
            public string Category;
            public string Size;
            public ToshibaCat(string output, string category, string size) { Output = output; Category = category; Size = size; }
        }

        private static readonly Dictionary<int, ToshibaCat> _ToshibaCatGeneric = new Dictionary<int, ToshibaCat>
        {
            // 通用版（未指定具體型號）：cat 207=列印大張各色 / 208=列印小張各色（不分 print/copy/fax/list）
            // 掃描分項：219/220=network, 221/222=copy, 223/224=fax
            { 207, new ToshibaCat("printed", "print",   "large")  },
            { 208, new ToshibaCat("printed", "print",   "normal") },
            { 219, new ToshibaCat("scanned", "network", "large")  }, { 220, new ToshibaCat("scanned", "network", "normal") },
            { 221, new ToshibaCat("scanned", "copy",    "large")  }, { 222, new ToshibaCat("scanned", "copy",    "normal") },
            { 223, new ToshibaCat("scanned", "fax",     "large")  }, { 224, new ToshibaCat("scanned", "fax",     "normal") },
        };

        private static readonly Dictionary<int, ToshibaCat> _ToshibaCat3515AC = new Dictionary<int, ToshibaCat>
        {
            { 209, new ToshibaCat("printed", "print",   "large")  }, { 210, new ToshibaCat("printed", "print",   "normal") },
            { 211, new ToshibaCat("printed", "copy",    "large")  }, { 212, new ToshibaCat("printed", "copy",    "normal") },
            { 215, new ToshibaCat("printed", "fax",     "large")  }, { 216, new ToshibaCat("printed", "fax",     "normal") },  // 3515AC 用 .216
            { 225, new ToshibaCat("printed", "list",    "large")  }, { 226, new ToshibaCat("printed", "list",    "normal") },  // 清單獨立 category=list
            { 219, new ToshibaCat("scanned", "network", "large")  }, { 220, new ToshibaCat("scanned", "network", "normal") },
            { 221, new ToshibaCat("scanned", "copy",    "large")  }, { 222, new ToshibaCat("scanned", "copy",    "normal") },
            { 223, new ToshibaCat("scanned", "fax",     "large")  }, { 224, new ToshibaCat("scanned", "fax",     "normal") },
        };

        private static readonly Dictionary<int, ToshibaCat> _ToshibaCat2555C = new Dictionary<int, ToshibaCat>
        {
            { 209, new ToshibaCat("printed", "print", "large")  }, { 210, new ToshibaCat("printed", "print", "normal") },
            { 211, new ToshibaCat("printed", "copy",  "large")  }, { 212, new ToshibaCat("printed", "copy",  "normal") },
            { 215, new ToshibaCat("printed", "fax",   "large")  }, { 218, new ToshibaCat("printed", "fax",   "normal") },  // 2555C 用 .218
            { 225, new ToshibaCat("printed", "list",  "large")  }, { 226, new ToshibaCat("printed", "list",  "normal") },  // 清單獨立 category=list
            { 219, new ToshibaCat("scanned", "network", "large")  }, { 220, new ToshibaCat("scanned", "network", "normal") },
            { 221, new ToshibaCat("scanned", "copy",  "large")  }, { 222, new ToshibaCat("scanned", "copy",  "normal") },
            { 223, new ToshibaCat("scanned", "fax",   "large")  }, { 224, new ToshibaCat("scanned", "fax",   "normal") },
        };

        private static string ToshibaColorName(int c)
        {
            if (c == 1) return "color_full";
            if (c == 2) return "duotone";
            if (c == 3) return "black";
            return null;
        }

        private void PopulateToshibaItems(Printer mfp, Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> counter, Dictionary<int, ToshibaCat> catMap)
        {
            // OID 尾段 .{cat}.1.{color} → 依 catMap 與 color 累計後寫入 mfp.items
            // agg key 用 "output|category|size|color" 字串避免 .NET 4.6.2 缺 ValueTuple
            var agg = new Dictionary<string, int>();
            foreach (var kvp in counter)
            {
                var parts = kvp.Key.ToString().Split('.');
                if (parts.Length < 3) continue;
                int cat, colorIdx, v;
                if (!int.TryParse(parts[parts.Length - 3], out cat)) continue;
                if (!int.TryParse(parts[parts.Length - 1], out colorIdx)) continue;
                ToshibaCat m;
                if (!catMap.TryGetValue(cat, out m)) continue;
                var colorName = ToshibaColorName(colorIdx);
                if (colorName == null) continue;
                if (!int.TryParse(kvp.Value == null ? "" : kvp.Value.ToString(), out v)) continue;
                var key = m.Output + "|" + m.Category + "|" + m.Size + "|" + colorName;
                int prev;
                agg[key] = (agg.TryGetValue(key, out prev) ? prev : 0) + v;
            }
            foreach (var kvp in agg)
            {
                if (kvp.Value <= 0) continue;
                var parts = kvp.Key.Split('|');
                mfp.items.Add(new DataClass.CounterItem
                {
                    output = parts[0],
                    category = parts[1],
                    size = parts[2],
                    color = parts[3],
                    sheets = kvp.Value
                });
            }
        }

        /// <summary>
        /// Toshiba 通用版（未指定具體型號）
        /// </summary>
        private bool Counter_Toshiba(Printer mfp)
        {
            var ip = mfp.ip;
            Trace(new LogInfo() { Category = "SNMP", Function = "Counter_Toshiba", Option = "request", Identity = ip });
            var counter = TOSHIBA_MIBs(ip);
            Trace(new LogInfo() { Category = "SNMP", Function = "Counter_Toshiba", Option = "response", Identity = ip, Message = $"{Newtonsoft.Json.JsonConvert.SerializeObject(counter)}" });
            if (counter == null) return true;
            mfp.snmp_data = Newtonsoft.Json.JsonConvert.SerializeObject(counter);
            PopulateToshibaItems(mfp, counter, _ToshibaCatGeneric);
            return Supplies_Toshiba(mfp);
        }

        /// <summary>
        /// Toshiba e-STUDIO 2555C 專用（fax 小張用 .218）
        /// </summary>
        private bool Counter_Toshiba_2555C(Printer mfp)
        {
            var ip = mfp.ip;
            Trace(new LogInfo() { Category = "SNMP", Function = "Counter_Toshiba_2555C", Option = "request", Identity = ip });
            var counter = TOSHIBA_MIBs(ip);
            Trace(new LogInfo() { Category = "SNMP", Function = "Counter_Toshiba_2555C", Option = "response", Identity = ip, Message = $"{Newtonsoft.Json.JsonConvert.SerializeObject(counter)}" });
            if (counter == null) return true;
            mfp.snmp_data = Newtonsoft.Json.JsonConvert.SerializeObject(counter);
            PopulateToshibaItems(mfp, counter, _ToshibaCat2555C);
            return Supplies_Toshiba(mfp);
        }

        /// <summary>
        /// Toshiba e-STUDIO 3515AC 專用（fax 小張用 .216）
        /// </summary>
        private bool Counter_Toshiba_3515AC(Printer mfp)
        {
            var ip = mfp.ip;
            Trace(new LogInfo() { Category = "SNMP", Function = "Counter_Toshiba_3515AC", Option = "request", Identity = ip });
            var counter = TOSHIBA_MIBs(ip);
            Trace(new LogInfo() { Category = "SNMP", Function = "Counter_Toshiba_3515AC", Option = "response", Identity = ip, Message = $"{Newtonsoft.Json.JsonConvert.SerializeObject(counter)}" });
            if (counter == null) return true;
            mfp.snmp_data = Newtonsoft.Json.JsonConvert.SerializeObject(counter);
            PopulateToshibaItems(mfp, counter, _ToshibaCat3515AC);
            return Supplies_Toshiba(mfp);
        }

        /// <summary>
        /// Kyocera（TASKalfa / ECOSYS 等）
        /// OID root: 1.3.6.1.4.1.1347.42.3.1
        ///   .1.1.1.1  Print B&W     .1.1.1.2  Print Color
        ///   .2.1.1.1  Copy  B&W     .2.1.1.2  Copy  Color
        ///   .3.1.1.1  FAX   B&W
        /// ※ 實際 OID 請以機器回傳值驗證後調整
        /// </summary>
        private bool Counter_Kyocera(Printer mfp)
        {
            var ip = mfp.ip;
            Trace(new LogInfo() { Category = "SNMP", Function = "Counter_Kyocera", Option = "request", Identity = ip });
            var counter = KYOCERA_MIBs(ip);
            Trace(new LogInfo() { Category = "SNMP", Function = "Counter_Kyocera", Option = "response", Identity = ip, Message = $"{Newtonsoft.Json.JsonConvert.SerializeObject(counter)}" });
            if (counter == null) return true;

            int printBlack = 0, printColor = 0;
            int copyBlack = 0, copyColor = 0;
            int faxBlack = 0;

            foreach (var obj in counter)
                switch (obj.Key.ToString())
                {
                    case "1.3.6.1.4.1.1347.42.3.1.1.1.1.1": int.TryParse(obj.Value.ToString(), out printBlack); break;
                    case "1.3.6.1.4.1.1347.42.3.1.1.1.1.2": int.TryParse(obj.Value.ToString(), out printColor); break;
                    case "1.3.6.1.4.1.1347.42.3.1.2.1.1.1": int.TryParse(obj.Value.ToString(), out copyBlack); break;
                    case "1.3.6.1.4.1.1347.42.3.1.2.1.1.2": int.TryParse(obj.Value.ToString(), out copyColor); break;
                    case "1.3.6.1.4.1.1347.42.3.1.3.1.1.1": int.TryParse(obj.Value.ToString(), out faxBlack); break;
                    default: break;
                }
            mfp.snmp_data = Newtonsoft.Json.JsonConvert.SerializeObject(counter);

            if (printBlack > 0) mfp.items.Add(new DataClass.CounterItem { output = "printed", category = "print", color = "black", size = "normal", sheets = printBlack });
            if (printColor > 0) mfp.items.Add(new DataClass.CounterItem { output = "printed", category = "print", color = "color_full", size = "normal", sheets = printColor });
            if (copyBlack > 0) mfp.items.Add(new DataClass.CounterItem { output = "printed", category = "copy", color = "black", size = "normal", sheets = copyBlack });
            if (copyColor > 0) mfp.items.Add(new DataClass.CounterItem { output = "printed", category = "copy", color = "color_full", size = "normal", sheets = copyColor });
            if (faxBlack > 0) mfp.items.Add(new DataClass.CounterItem { output = "printed", category = "fax", color = "black", size = "normal", sheets = faxBlack });

            return Supplies_Kyocera(mfp);
        }

        #endregion

        #region Supplies

        /// <summary>
        /// 將 capacity / level 值換算為百分比，直接加入 mfp.supply_items。
        /// capacity = 0 表示該耗材不存在，略過。
        /// </summary>
        private static void AddSupply(Printer mfp, string type, string color, int level, int capacity)
        {
            if (capacity > 0)
                mfp.supply_items.Add(new DataClass.SupplyItem
                {
                    type = type,
                    color = color,
                    level = Math.Round((double)level / capacity, 2)
                });
        }

        // ── Supplies（Common / Ricoh / Xerox / Ricoh IMC）────────────────────
        // OID 索引對應：.1=toner_black / .2=toner_waste / .3=cyan / .4=magenta / .5=yellow
        private bool Supplies(Printer mfp)
        {
            var ip = mfp.ip;
            Trace(new LogInfo() { Category = "SNMP", Function = "Supplies", Option = "request", Identity = "capacity", Message = $"None" });
            var capacity = Printer_MaxCapacity(ip);
            Trace(new LogInfo() { Category = "SNMP", Function = "Supplies", Option = "response", Identity = "capacity", Message = $"{Newtonsoft.Json.JsonConvert.SerializeObject(capacity)}" });
            if (capacity == null) return true;
            int cap1 = 0, cap2 = 0, cap3 = 0, cap4 = 0, cap5 = 0;
            foreach (var obj in capacity)
                switch (obj.Key.ToString())
                {
                    case "1.3.6.1.2.1.43.11.1.1.8.1.1": int.TryParse(obj.Value.ToString(), out cap1); break; // toner_black
                    case "1.3.6.1.2.1.43.11.1.1.8.1.2": int.TryParse(obj.Value.ToString(), out cap2); break; // toner_waste
                    case "1.3.6.1.2.1.43.11.1.1.8.1.3": int.TryParse(obj.Value.ToString(), out cap3); break; // toner_cyan
                    case "1.3.6.1.2.1.43.11.1.1.8.1.4": int.TryParse(obj.Value.ToString(), out cap4); break; // toner_magenta
                    case "1.3.6.1.2.1.43.11.1.1.8.1.5": int.TryParse(obj.Value.ToString(), out cap5); break; // toner_yellow
                    default: break;
                }

            Trace(new LogInfo() { Category = "SNMP", Function = "Supplies", Option = "request", Identity = "level", Message = $"None" });
            var level = Printer_SuppliesLevel(ip);
            Trace(new LogInfo() { Category = "SNMP", Function = "Supplies", Option = "response", Identity = "level", Message = $"{Newtonsoft.Json.JsonConvert.SerializeObject(level)}" });
            if (level == null) return true;
            int lev1 = 0, lev2 = 0, lev3 = 0, lev4 = 0, lev5 = 0;
            foreach (var obj in level)
                switch (obj.Key.ToString())
                {
                    case "1.3.6.1.2.1.43.11.1.1.9.1.1": int.TryParse(obj.Value.ToString(), out lev1); break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.2": int.TryParse(obj.Value.ToString(), out lev2); break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.3": int.TryParse(obj.Value.ToString(), out lev3); break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.4": int.TryParse(obj.Value.ToString(), out lev4); break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.5": int.TryParse(obj.Value.ToString(), out lev5); break;
                    default: break;
                }

            AddSupply(mfp, "toner", "black", lev1, cap1);
            AddSupply(mfp, "toner", "waste", lev2, cap2);
            AddSupply(mfp, "toner", "cyan", lev3, cap3);
            AddSupply(mfp, "toner", "magenta", lev4, cap4);
            AddSupply(mfp, "toner", "yellow", lev5, cap5);
            return false;
        }

        // ── Supplies_Kyocera ──────────────────────────────────────────────────
        // OID 索引對應（Kyocera 順序不同）：.4=toner_black / .5=waste / .1=cyan / .2=magenta / .3=yellow
        private bool Supplies_Kyocera(Printer mfp)
        {
            var ip = mfp.ip;
            Trace(new LogInfo() { Category = "SNMP", Function = "Supplies", Option = "request", Identity = "capacity", Message = $"None" });
            var capacity = Printer_MaxCapacity(ip);
            Trace(new LogInfo() { Category = "SNMP", Function = "Supplies", Option = "response", Identity = "capacity", Message = $"{Newtonsoft.Json.JsonConvert.SerializeObject(capacity)}" });
            if (capacity == null) return true;
            int capBlack = 0, capWaste = 0, capCyan = 0, capMagenta = 0, capYellow = 0;
            foreach (var obj in capacity)
                switch (obj.Key.ToString())
                {
                    case "1.3.6.1.2.1.43.11.1.1.8.1.4": int.TryParse(obj.Value.ToString(), out capBlack); break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.5": int.TryParse(obj.Value.ToString(), out capWaste); break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.1": int.TryParse(obj.Value.ToString(), out capCyan); break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.2": int.TryParse(obj.Value.ToString(), out capMagenta); break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.3": int.TryParse(obj.Value.ToString(), out capYellow); break;
                    default: break;
                }

            Trace(new LogInfo() { Category = "SNMP", Function = "Supplies", Option = "request", Identity = "level", Message = $"None" });
            var level = Printer_SuppliesLevel(ip);
            Trace(new LogInfo() { Category = "SNMP", Function = "Supplies", Option = "response", Identity = "level", Message = $"{Newtonsoft.Json.JsonConvert.SerializeObject(level)}" });
            if (level == null) return true;
            int levBlack = 0, levWaste = 0, levCyan = 0, levMagenta = 0, levYellow = 0;
            foreach (var obj in level)
                switch (obj.Key.ToString())
                {
                    case "1.3.6.1.2.1.43.11.1.1.9.1.4": int.TryParse(obj.Value.ToString(), out levBlack); break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.5": int.TryParse(obj.Value.ToString(), out levWaste); break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.1": int.TryParse(obj.Value.ToString(), out levCyan); break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.2": int.TryParse(obj.Value.ToString(), out levMagenta); break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.3": int.TryParse(obj.Value.ToString(), out levYellow); break;
                    default: break;
                }

            AddSupply(mfp, "toner", "black", levBlack, capBlack);
            AddSupply(mfp, "toner", "waste", levWaste, capWaste);
            AddSupply(mfp, "toner", "cyan", levCyan, capCyan);
            AddSupply(mfp, "toner", "magenta", levMagenta, capMagenta);
            AddSupply(mfp, "toner", "yellow", levYellow, capYellow);
            return false;
        }

        // ── Supplies_Toshiba ──────────────────────────────────────────────────
        // OID 索引對應（Toshiba 順序）：.1=toner_black / .2=cyan / .3=magenta / .4=yellow / .5=waste
        private bool Supplies_Toshiba(Printer mfp)
        {
            var ip = mfp.ip;
            Trace(new LogInfo() { Category = "SNMP", Function = "Supplies", Option = "request", Identity = "capacity", Message = $"None" });
            var capacity = Printer_MaxCapacity(ip);
            Trace(new LogInfo() { Category = "SNMP", Function = "Supplies", Option = "response", Identity = "capacity", Message = $"{Newtonsoft.Json.JsonConvert.SerializeObject(capacity)}" });
            if (capacity == null) return true;
            int capBlack = 0, capCyan = 0, capMagenta = 0, capYellow = 0, capWaste = 0;
            foreach (var obj in capacity)
                switch (obj.Key.ToString())
                {
                    case "1.3.6.1.2.1.43.11.1.1.8.1.1": int.TryParse(obj.Value.ToString(), out capBlack); break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.2": int.TryParse(obj.Value.ToString(), out capCyan); break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.3": int.TryParse(obj.Value.ToString(), out capMagenta); break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.4": int.TryParse(obj.Value.ToString(), out capYellow); break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.5": int.TryParse(obj.Value.ToString(), out capWaste); break;
                    default: break;
                }

            Trace(new LogInfo() { Category = "SNMP", Function = "Supplies", Option = "request", Identity = "level", Message = $"None" });
            var level = Printer_SuppliesLevel(ip);
            Trace(new LogInfo() { Category = "SNMP", Function = "Supplies", Option = "response", Identity = "level", Message = $"{Newtonsoft.Json.JsonConvert.SerializeObject(level)}" });
            if (level == null) return true;
            int levBlack = 0, levCyan = 0, levMagenta = 0, levYellow = 0, levWaste = 0;
            foreach (var obj in level)
                switch (obj.Key.ToString())
                {
                    case "1.3.6.1.2.1.43.11.1.1.9.1.1": int.TryParse(obj.Value.ToString(), out levBlack); break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.2": int.TryParse(obj.Value.ToString(), out levCyan); break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.3": int.TryParse(obj.Value.ToString(), out levMagenta); break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.4": int.TryParse(obj.Value.ToString(), out levYellow); break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.5": int.TryParse(obj.Value.ToString(), out levWaste); break;
                    default: break;
                }

            AddSupply(mfp, "toner", "black", levBlack, capBlack);
            AddSupply(mfp, "toner", "cyan", levCyan, capCyan);
            AddSupply(mfp, "toner", "magenta", levMagenta, capMagenta);
            AddSupply(mfp, "toner", "yellow", levYellow, capYellow);
            AddSupply(mfp, "toner", "waste", levWaste, capWaste);
            return false;
        }
        #endregion

        #region SNMP
        //SNMPv1 get、getnext、set、getresponse、trap 
        //SNMPv2 get、getnext、set、getresponse、trap、getbulk、notification、ínform、report
        //SNMPv3 get、getnext、set、getresponse、trap、getbulk、notification、ínform、report

        //==================== GetBulk ====================
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> SNMPv3_GetBulk(string ip, string oid, string description = null)
        {
            Console.WriteLine($"==================== {description ?? "SNMPv3_GetBulk"} ====================");
            var dics = new Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType>();

            //==================== Init ====================

            //========== SecureAgent ==========
            var param = new SnmpSharpNet.SecureAgentParameters();
            param.SecurityName.Set(new SnmpSharpNet.OctetString("admin"));
            //param.Authentication = SnmpSharpNet.AuthenticationDigests.None;
            //param.Privacy = SnmpSharpNet.PrivacyProtocols.None;
            // optional: make sure you request REPORT packet return on errors
            param.Reportable = false;

            //========== Authentication ==========
            // Set the authentication digest
            param.Authentication = SnmpSharpNet.AuthenticationDigests.MD5;
            // Set the authentication password
            //param.AuthenticationSecret.Set("password");
            param.AuthenticationSecret.Set("admin");

            //========== Privacy ==========
            // Set the privacy protocol
            param.Privacy = SnmpSharpNet.PrivacyProtocols.DES;
            // Set the privacy password
            //param.PrivacySecret.Set("CryptoPassword");
            param.PrivacySecret.Set("admin");


            //========== Pdu ==========
            //var pdu = new SnmpSharpNet.ScopedPdu(SnmpSharpNet.PduType.GetBulk);
            //pdu.NonRepeaters = 0;
            //pdu.MaxRepetitions = 5;
            var pdu = new SnmpSharpNet.Pdu();
            // Set the request type (default is GET)
            pdu.Type = SnmpSharpNet.PduType.Get;
            // Add variables you wish to query
            pdu.VbList.Clear();
            pdu.VbList.Add("1.3.6.1.2.1.43.5.1.1.16.1");

            System.Net.IPAddress.TryParse(ip, out var ip_addr);

            using (var target = new SnmpSharpNet.UdpTarget(ip_addr, 161, 2000, 1))
            {
                if (!target.Discovery(param))
                {
                    Console.WriteLine("Discovery failed. Unable to continue...");
                    target.Close();
                    return dics;
                }

                var result = (SnmpSharpNet.SnmpV3Packet)target.Request(pdu, param);
                if (result != null)
                {
                    if (result.Pdu.Type == SnmpSharpNet.PduType.Report)
                    {
                        Console.WriteLine("Report packet received.");
                        string errstr = SnmpSharpNet.SNMPV3ReportError.TranslateError(result);
                        Console.WriteLine("Error: {0}", errstr);
                    }
                }
                //var rootOid = new SnmpSharpNet.Oid(oid); // 根 OID
                //var lastOid = (SnmpSharpNet.Oid)rootOid.Clone();

                //while (lastOid != null)
                //{
                //    try
                //    {
                //        if (pdu.RequestId != 0) { pdu.RequestId += 1; }
                //        pdu.VbList.Clear();
                //        pdu.VbList.Add(lastOid);

                //        var result = (SnmpSharpNet.SnmpV3Packet)target.Request(pdu, param);

                //        if (result != null)
                //        {
                //            if (result.Pdu.Type == SnmpSharpNet.PduType.Report)
                //            {
                //                Console.WriteLine("Report packet received.");
                //                string errstr = SnmpSharpNet.SNMPV3ReportError.TranslateError(result);
                //                Console.WriteLine("Error: {0}", errstr);
                //                break;
                //            }

                //            if (result.Pdu.ErrorStatus != 0)
                //            {
                //                Console.WriteLine($"SNMP Error: {result.Pdu.ErrorIndex}/{result.Pdu.ErrorStatus}");
                //                lastOid = null;
                //                break;
                //            }
                //            else
                //            {
                //                foreach (var v in result.Pdu.VbList)
                //                {
                //                    if (rootOid.IsRootOf(v.Oid))
                //                    {
                //                        dics.Add(v.Oid, v.Value);
                //                        Console.WriteLine($"Oid: {v.Oid.ToString().PadRight(60)} \t TypeName:{SnmpSharpNet.SnmpConstants.GetTypeName(v.Value.Type).PadRight(20)} \t Value:{v.Value}");
                //                        lastOid = v.Oid;
                //                        if (v.Value.ToString() == "SNMP End-of-MIB-View")
                //                        {
                //                            Console.WriteLine("========== SNMP End-of-MIB-View ==========");
                //                            lastOid = null;
                //                            break;
                //                        }
                //                    }
                //                    else
                //                    {
                //                        lastOid = null;
                //                    }
                //                }
                //            }
                //        }
                //        else
                //        {
                //            Console.WriteLine("No response received from SNMP agent.");
                //            lastOid = null;
                //        }
                //    }
                //    catch (Exception ex)
                //    {
                //        Console.WriteLine($"Exception: {ex.Message}");
                //        lastOid = null;
                //    }
                //}
            }
            Console.WriteLine($"==================== {description ?? "SNMPv3_GetBulk"} End ====================");
            return dics;
        }
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> SNMPv2_GetBulk(string ip, string oid, string description = null)
        {
            Console.WriteLine($"==================== {description ?? "SNMPv2_GetBulk"} ====================");
            var dics = new Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType>();

            //==================== Init ====================

            //========== Agent ==========
            var param = new SnmpSharpNet.AgentParameters();
            param.Version = SnmpSharpNet.SnmpVersion.Ver2;
            param.Community.Set("public");

            //========== Pdu ==========
            var pdu = new SnmpSharpNet.Pdu();
            pdu.Type = SnmpSharpNet.PduType.GetBulk;
            pdu.NonRepeaters = 0;
            pdu.MaxRepetitions = 5;
            pdu.VbList.Clear();

            System.Net.IPAddress.TryParse(ip, out var ip_addr);
            using (var target = new SnmpSharpNet.UdpTarget(ip_addr, 161, 2000, 1))
            {
                // Define Oid that is the root of the MIB
                //  tree you wish to retrieve
                var rootOid = new SnmpSharpNet.Oid(oid); // ifDescr

                // This Oid represents last Oid returned by
                //  the SNMP agent
                var lastOid = (SnmpSharpNet.Oid)rootOid.Clone();
                // Make SNMP request
                while (lastOid != null)
                {
                    try
                    {
                        // When Pdu class is first constructed, RequestId is set to 0
                        // and during encoding id will be set to the random value
                        // for subsequent requests, id will be set to a value that
                        // needs to be incremented to have unique request ids for each
                        // packet
                        if (pdu.RequestId != 0) { pdu.RequestId += 1; }                // Clear Oids from the Pdu class.
                        pdu.VbList.Clear();
                        // Initialize request PDU with the last retrieved Oid
                        pdu.VbList.Add(lastOid);
                        // Make SNMP request
                        var result = (SnmpSharpNet.SnmpV2Packet)target.Request(pdu, param);
                        System.Threading.Thread.Sleep(10);

                        // You should catch exceptions in the Request if using in real application.

                        // If result is null then agent didn't reply or we couldn't parse the reply.
                        if (result != null)
                        {
                            // ErrorStatus other then 0 is an error returned by 
                            // the Agent - see SnmpConstants for error definitions
                            if (result.Pdu.ErrorStatus != 0)
                            {
                                Console.WriteLine($"SNMP Error: {result?.Pdu?.ErrorIndex}/{result?.Pdu?.ErrorStatus}");
                                lastOid = null;
                                break;
                            }
                            else
                            {

                                // Walk through returned variable bindings
                                foreach (var v in result.Pdu.VbList)
                                {
                                    // Check that retrieved Oid is "child" of the root OID
                                    if (rootOid.IsRootOf(v.Oid))
                                    {
                                        dics.Add(v.Oid, v.Value);
                                        Console.WriteLine($"Oid: {v.Oid.ToString().PadRight(60)} \t TypeName:{SnmpSharpNet.SnmpConstants.GetTypeName(v.Value.Type).PadRight(20)} \t Value:{v.Value}");
                                        lastOid = v.Oid;
                                        if (v.Value.ToString() == "SNMP End-of-MIB-View") { Console.WriteLine("========== SNMP End-of-MIB-View =========="); break; }
                                    }
                                    else lastOid = null;
                                }
                            }
                        }
                        else { Console.WriteLine("No response received from SNMP agent."); }
                    }
                    catch (Exception ex) { Console.WriteLine($"Exception:{ex.Message}"); }
                }
            }
            Console.WriteLine($"==================== {description ?? "SNMPv2_GetBulk"} End ====================");
            return dics;
        }
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> SNMPv1_GetBulk(string ip, string oid, string description = null)
        {
            Console.WriteLine($"==================== {description ?? "SNMPv1_GetBulk"} ====================");
            var dics = new Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType>();

            //==================== Init ====================

            //========== Agent ==========
            var param = new SnmpSharpNet.AgentParameters();
            param.Version = SnmpSharpNet.SnmpVersion.Ver1;
            param.Community.Set("public");

            //========== Pdu ==========
            var pdu = new SnmpSharpNet.Pdu();
            pdu.Type = SnmpSharpNet.PduType.GetNext;

            System.Net.IPAddress.TryParse(ip, out var ip_addr);

            using (var target = new SnmpSharpNet.UdpTarget(ip_addr, 161, 2000, 1))
            {
                var rootOid = new SnmpSharpNet.Oid(oid); // 根 OID
                var lastOid = (SnmpSharpNet.Oid)rootOid.Clone();

                while (lastOid != null)
                {
                    try
                    {
                        // 创建 PDU
                        pdu.VbList.Clear();
                        pdu.VbList.Add(lastOid);
                        // 发送请求
                        var result = (SnmpSharpNet.SnmpV1Packet)target.Request(pdu, param);

                        if (result != null)
                        {
                            if (result.Pdu.ErrorStatus != 0)
                            {
                                Console.WriteLine($"SNMP Error: {result.Pdu.ErrorStatus} at index {result.Pdu.ErrorIndex}");
                                lastOid = null;
                                break;
                            }
                            else
                            {
                                foreach (var v in result.Pdu.VbList)
                                {
                                    if (rootOid.IsRootOf(v.Oid))
                                    {
                                        dics.Add(v.Oid, v.Value);
                                        Console.WriteLine($"Oid: {v.Oid.ToString().PadRight(60)} \t TypeName: {SnmpSharpNet.SnmpConstants.GetTypeName(v.Value.Type).PadRight(20)} \t Value: {v.Value}");
                                        lastOid = v.Oid;
                                    }
                                    else
                                    {
                                        lastOid = null;
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("No response received from SNMP agent.");
                            lastOid = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception: {ex.Message}");
                        lastOid = null;
                    }
                }
            }
            Console.WriteLine($"==================== {description ?? "SNMPv1_GetBulk"} End ====================");
            return dics;
        }
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> Printer_MAC(string ip) => SNMPv1_GetBulk(ip, "1.3.6.1.2.1.2.2.1.6", "MAC");
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> Printer_PrinterName(string ip) => SNMPv1_GetBulk(ip, "1.3.6.1.2.1.43.5.1.1.16", "PrinterName");
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> Printer_SerialNumber(string ip) => SNMPv1_GetBulk(ip, "1.3.6.1.2.1.43.5.1.1.17", "SerialNumber");
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> Printer_MaxCapacity(string ip) => SNMPv1_GetBulk(ip, "1.3.6.1.2.1.43.11.1.1.8", "MaxCapacity");
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> Printer_SuppliesLevel(string ip) => SNMPv1_GetBulk(ip, "1.3.6.1.2.1.43.11.1.1.9", "SuppliesLevel");
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> Printer_SuppliesDescription(string ip) => SNMPv1_GetBulk(ip, "1.3.6.1.2.1.43.11.1.1.6", "SuppliesDescription");
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> Printer_Alert(string ip) => SNMPv1_GetBulk(ip, "1.3.6.1.2.1.43.18.1.1", "Printer_Alert");
        //private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> Printer_AlertDescription(string ip) => SNMP_GetBulk(ip, "1.3.6.1.2.1.43.18.1.1.8.1", "Printer_Alert");

        //==================== Get ====================
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> SNMPv2_Get(string ip, string oid, string description = null)
        {
            Console.WriteLine($"==================== {description ?? "SNMPv2_Get"} ====================");
            var dics = new Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType>();

            //==================== Init ====================

            //========== Agent ==========
            var param = new SnmpSharpNet.AgentParameters();
            param.Version = SnmpSharpNet.SnmpVersion.Ver2;
            param.Community.Set("public");

            //========== Pdu ==========
            var pdu = new SnmpSharpNet.Pdu();
            pdu.Type = SnmpSharpNet.PduType.Get;

            System.Net.IPAddress.TryParse(ip, out var ip_addr);
            using (var target = new SnmpSharpNet.UdpTarget(ip_addr, 161, 2000, 1))
            {
                // Define Oid that is the root of the MIB
                //  tree you wish to retrieve
                var rootOid = new SnmpSharpNet.Oid(oid); // ifDescr

                // This Oid represents last Oid returned by
                //  the SNMP agent
                var lastOid = (SnmpSharpNet.Oid)rootOid.Clone();
                // Make SNMP request

                try
                {
                    // When Pdu class is first constructed, RequestId is set to 0
                    // and during encoding id will be set to the random value
                    // for subsequent requests, id will be set to a value that
                    // needs to be incremented to have unique request ids for each
                    // packet
                    if (pdu.RequestId != 0) { pdu.RequestId += 1; }                // Clear Oids from the Pdu class.
                    pdu.VbList.Clear();
                    // Initialize request PDU with the last retrieved Oid
                    pdu.VbList.Add(lastOid);
                    // Make SNMP request
                    var result = (SnmpSharpNet.SnmpV2Packet)target.Request(pdu, param);
                    System.Threading.Thread.Sleep(10);

                    // You should catch exceptions in the Request if using in real application.

                    // If result is null then agent didn't reply or we couldn't parse the reply.
                    if (result != null)
                    {
                        // ErrorStatus other then 0 is an error returned by 
                        // the Agent - see SnmpConstants for error definitions
                        if (result.Pdu.ErrorStatus != 0)
                        {
                            Console.WriteLine($"SNMP Error: {result?.Pdu?.ErrorIndex}/{result?.Pdu?.ErrorStatus}");
                        }
                        else
                        {
                            // Walk through returned variable bindings
                            foreach (var v in result.Pdu.VbList)
                            {
                                // Check that retrieved Oid is "child" of the root OID
                                if (rootOid.IsRootOf(v.Oid))
                                {
                                    dics.Add(v.Oid, v.Value);
                                    Console.WriteLine($"Oid:{v.Oid.ToString().PadRight(60)} \t TypeName:{SnmpSharpNet.SnmpConstants.GetTypeName(v.Value.Type).PadRight(20)} \t Value:{v.Value}");
                                    lastOid = v.Oid;
                                    if (v.Value.ToString() == "SNMP End-of-MIB-View") { Console.WriteLine("========== SNMP End-of-MIB-View =========="); break; }
                                }
                            }
                        }
                    }
                    else { Console.WriteLine("No response received from SNMP agent."); }
                }
                catch (Exception ex) { Console.WriteLine($"Exception:{ex.Message}"); }
            }
            Console.WriteLine($"==================== {description ?? "SNMPv2_Get"} End ====================");
            return dics;
        }
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> SNMPv1_Get(string ip, string oid, string description = null)
        {
            Console.WriteLine($"==================== {description ?? "SNMPv1_Get"} ====================");
            var dics = new Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType>();

            //==================== Init ====================

            //========== Agent ==========
            var param = new SnmpSharpNet.AgentParameters();
            param.Version = SnmpSharpNet.SnmpVersion.Ver1;
            param.Community.Set("public");

            //========== Pdu ==========
            var pdu = new SnmpSharpNet.Pdu();
            pdu.Type = SnmpSharpNet.PduType.Get;

            System.Net.IPAddress.TryParse(ip, out var ip_addr);
            using (var target = new SnmpSharpNet.UdpTarget(ip_addr, 161, 2000, 1))
            {
                // Define Oid that is the root of the MIB
                //  tree you wish to retrieve
                var rootOid = new SnmpSharpNet.Oid(oid); // ifDescr

                // This Oid represents last Oid returned by
                //  the SNMP agent
                var lastOid = (SnmpSharpNet.Oid)rootOid.Clone();
                // Make SNMP request

                try
                {
                    // When Pdu class is first constructed, RequestId is set to 0
                    // and during encoding id will be set to the random value
                    // for subsequent requests, id will be set to a value that
                    // needs to be incremented to have unique request ids for each
                    // packet
                    if (pdu.RequestId != 0) { pdu.RequestId += 1; }                // Clear Oids from the Pdu class.
                    pdu.VbList.Clear();
                    // Initialize request PDU with the last retrieved Oid
                    pdu.VbList.Add(lastOid);
                    // Make SNMP request
                    var result = (SnmpSharpNet.SnmpV1Packet)target.Request(pdu, param);
                    System.Threading.Thread.Sleep(10);

                    // You should catch exceptions in the Request if using in real application.

                    // If result is null then agent didn't reply or we couldn't parse the reply.
                    if (result != null)
                    {
                        // ErrorStatus other then 0 is an error returned by 
                        // the Agent - see SnmpConstants for error definitions
                        if (result.Pdu.ErrorStatus != 0)
                        {
                            Console.WriteLine($"SNMP Error: {result?.Pdu?.ErrorIndex}/{result?.Pdu?.ErrorStatus}");
                        }
                        else
                        {
                            // Walk through returned variable bindings
                            foreach (var v in result.Pdu.VbList)
                            {
                                // Check that retrieved Oid is "child" of the root OID
                                if (rootOid.IsRootOf(v.Oid))
                                {
                                    dics.Add(v.Oid, v.Value);
                                    Console.WriteLine($"Oid:{v.Oid.ToString().PadRight(60)} \t TypeName:{SnmpSharpNet.SnmpConstants.GetTypeName(v.Value.Type).PadRight(20)} \t Value:{v.Value}");
                                    lastOid = v.Oid;
                                    if (v.Value.ToString() == "SNMP End-of-MIB-View") { Console.WriteLine("========== SNMP End-of-MIB-View =========="); break; }
                                }
                            }
                        }
                    }
                    else { Console.WriteLine("No response received from SNMP agent."); }
                }
                catch (Exception ex) { Console.WriteLine($"Exception:{ex.Message}"); }
            }
            Console.WriteLine($"==================== {description ?? "SNMPv1_Get"} End ====================");
            return dics;
        }

        //1.3.6.1.2.1.2.2.1.6

        //==================== GetNext ====================
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> SNMPv2_GetNext(string ip, string oid, string description = null)
        {
            Console.WriteLine($"==================== {description ?? "SNMPv2_GetNext"} ====================");
            var dics = new Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType>();

            //==================== Init ====================

            //========== Agent ==========
            var param = new SnmpSharpNet.AgentParameters();
            param.Version = SnmpSharpNet.SnmpVersion.Ver2;
            param.Community.Set("public");

            //========== Pdu ==========
            var pdu = new SnmpSharpNet.Pdu();
            pdu.Type = SnmpSharpNet.PduType.GetNext;

            System.Net.IPAddress.TryParse(ip, out var ip_addr);
            using (var target = new SnmpSharpNet.UdpTarget(ip_addr, 161, 2000, 1))
            {
                // Define Oid that is the root of the MIB
                //  tree you wish to retrieve
                var rootOid = new SnmpSharpNet.Oid(oid); // ifDescr

                // This Oid represents last Oid returned by
                //  the SNMP agent
                var lastOid = (SnmpSharpNet.Oid)rootOid.Clone();
                // Make SNMP request

                try
                {
                    // When Pdu class is first constructed, RequestId is set to 0
                    // and during encoding id will be set to the random value
                    // for subsequent requests, id will be set to a value that
                    // needs to be incremented to have unique request ids for each
                    // packet
                    if (pdu.RequestId != 0) { pdu.RequestId += 1; }                // Clear Oids from the Pdu class.
                    pdu.VbList.Clear();
                    // Initialize request PDU with the last retrieved Oid
                    pdu.VbList.Add(lastOid);
                    // Make SNMP request
                    var result = (SnmpSharpNet.SnmpV1Packet)target.Request(pdu, param);
                    System.Threading.Thread.Sleep(10);

                    // You should catch exceptions in the Request if using in real application.

                    // If result is null then agent didn't reply or we couldn't parse the reply.
                    if (result != null)
                    {
                        // ErrorStatus other then 0 is an error returned by 
                        // the Agent - see SnmpConstants for error definitions
                        if (result.Pdu.ErrorStatus != 0)
                        {
                            Console.WriteLine($"SNMP Error: {result?.Pdu?.ErrorIndex}/{result?.Pdu?.ErrorStatus}");
                        }
                        else
                        {
                            // Walk through returned variable bindings
                            foreach (var v in result.Pdu.VbList)
                            {
                                // Check that retrieved Oid is "child" of the root OID
                                if (rootOid.IsRootOf(v.Oid))
                                {
                                    dics.Add(v.Oid, v.Value);
                                    Console.WriteLine($"Oid:{v.Oid.ToString().PadRight(60)} \t TypeName:{SnmpSharpNet.SnmpConstants.GetTypeName(v.Value.Type).PadRight(20)} \t Value:{v.Value}");
                                    lastOid = v.Oid;
                                    if (v.Value.ToString() == "SNMP End-of-MIB-View") { Console.WriteLine("========== SNMP End-of-MIB-View =========="); break; }
                                }
                            }
                        }
                    }
                    else { Console.WriteLine("No response received from SNMP agent."); }
                }
                catch (Exception ex) { Console.WriteLine($"Exception:{ex.Message}"); }
            }
            Console.WriteLine($"==================== {description ?? "SNMPv2_GetNext"} End ====================");
            return dics;
        }
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> SNMPv1_GetNext(string ip, string oid, string description = null)
        {
            Console.WriteLine($"==================== {description ?? "SNMPv1_GetNext"} ====================");
            var dics = new Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType>();

            //==================== Init ====================

            //========== Agent ==========
            var param = new SnmpSharpNet.AgentParameters();
            param.Version = SnmpSharpNet.SnmpVersion.Ver1;
            param.Community.Set("public");

            //========== Pdu ==========
            var pdu = new SnmpSharpNet.Pdu();
            pdu.Type = SnmpSharpNet.PduType.GetNext;

            System.Net.IPAddress.TryParse(ip, out var ip_addr);
            using (var target = new SnmpSharpNet.UdpTarget(ip_addr, 161, 2000, 1))
            {
                // Define Oid that is the root of the MIB
                //  tree you wish to retrieve
                var rootOid = new SnmpSharpNet.Oid(oid); // ifDescr

                // This Oid represents last Oid returned by
                //  the SNMP agent
                var lastOid = (SnmpSharpNet.Oid)rootOid.Clone();
                // Make SNMP request

                try
                {
                    // When Pdu class is first constructed, RequestId is set to 0
                    // and during encoding id will be set to the random value
                    // for subsequent requests, id will be set to a value that
                    // needs to be incremented to have unique request ids for each
                    // packet
                    if (pdu.RequestId != 0) { pdu.RequestId += 1; }                // Clear Oids from the Pdu class.
                    pdu.VbList.Clear();
                    // Initialize request PDU with the last retrieved Oid
                    pdu.VbList.Add(lastOid);
                    // Make SNMP request
                    var result = (SnmpSharpNet.SnmpV1Packet)target.Request(pdu, param);
                    System.Threading.Thread.Sleep(10);

                    // You should catch exceptions in the Request if using in real application.

                    // If result is null then agent didn't reply or we couldn't parse the reply.
                    if (result != null)
                    {
                        // ErrorStatus other then 0 is an error returned by 
                        // the Agent - see SnmpConstants for error definitions
                        if (result.Pdu.ErrorStatus != 0)
                        {
                            Console.WriteLine($"SNMP Error: {result?.Pdu?.ErrorIndex}/{result?.Pdu?.ErrorStatus}");
                        }
                        else
                        {

                            // Walk through returned variable bindings
                            foreach (var v in result.Pdu.VbList)
                            {
                                // Check that retrieved Oid is "child" of the root OID
                                if (rootOid.IsRootOf(v.Oid))
                                {
                                    dics.Add(v.Oid, v.Value);
                                    Console.WriteLine($"Oid:{v.Oid.ToString().PadRight(60)} \t TypeName:{SnmpSharpNet.SnmpConstants.GetTypeName(v.Value.Type).PadRight(20)} \t Value:{v.Value}");
                                    lastOid = v.Oid;
                                    if (v.Value.ToString() == "SNMP End-of-MIB-View") { Console.WriteLine("========== SNMP End-of-MIB-View =========="); break; }
                                }
                            }
                        }
                    }
                    else { Console.WriteLine("No response received from SNMP agent."); }
                }
                catch (Exception ex) { Console.WriteLine($"Exception:{ex.Message}"); }
            }
            Console.WriteLine($"==================== {description ?? "SNMPv1_GetNext"} End ====================");
            return dics;
        }
        #endregion

        #region 私人MIBs
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> COMMON_MIBs(string ip) => SNMPv1_GetBulk(ip, "1.3.6.1.2.1.43.10.2.1.4.1.1", "COMMON_MIBs");
        /*
         * ※數值      1.3.6.1.4.1.253.8.53.13.2.1.6.1.20
         * ※內容描述  1.3.6.1.4.1.253.8.53.13.2.1.8
         * ※xcmHrDevDetailValueString = 1.3.6.1.4.1.253.8.53.13.2.1.6.1.20
         * ========================================
         * xcmHrDevDetailValueString.1.20.1		    Total Impressions               [總張數]
         * xcmHrDevDetailValueString.1.20.2		    Power On Impressions
         * xcmHrDevDetailValueString.1.20.7		    Black Printed Impressions       [黑白 總列印]
         * xcmHrDevDetailValueString.1.20.8		    Black Printed Sheets            [黑白 單面]
         * xcmHrDevDetailValueString.1.20.9		    Black Printed 2 Sided Sheets    [黑白 雙面]
         * xcmHrDevDetailValueString.1.20.10		Black Printed Large Sheets      [黑白 大張]
         * xcmHrDevDetailValueString.1.20.15		Printed Sheets                  [列印 單面]
         * xcmHrDevDetailValueString.1.20.16		Printed 2 Sided Sheets          [列印 雙面]
         * xcmHrDevDetailValueString.1.20.29		Color Printed Impressions       [列印 彩色 總張數]
         * xcmHrDevDetailValueString.1.20.30		Color Printed Sheets            [彩色 單面]
         * xcmHrDevDetailValueString.1.20.31		Color Printed 2 Sided Sheets    [彩色 雙面]
         * xcmHrDevDetailValueString.1.20.32		Color Printed Large Sheets      [彩色 大張]
         * xcmHrDevDetailValueString.1.20.33		Color Impressions               [列印+複印=彩色 總張數]
         * xcmHrDevDetailValueString.1.20.34		Black Impressions               [列印+複印=黑白 總張數]
         * xcmHrDevDetailValueString.1.20.38		Sheets                          [列印+複印=總張數]
         * xcmHrDevDetailValueString.1.20.39		2 Sided Sheets                  [雙面]
         * xcmHrDevDetailValueString.1.20.43		Color Large Impressions         [彩色 大張 總張數]
         * xcmHrDevDetailValueString.1.20.44		Black Large Impressions         [黑白 大張 總張數]
         * xcmHrDevDetailValueString.1.20.47		Large Impressions               [大張 總張數]
         * xcmHrDevDetailValueString.1.20.71		Fax Impressions                 [傳真 總張數]
         */
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> XEROX_MIBs(string ip) => SNMPv1_GetBulk(ip, "1.3.6.1.4.1.253.8.53.13.2.1.6.1.20", "XEROX_MIBs");
        /*
         * ※數值      1.3.6.1.4.1.367.3.2.1.2.19.5.1.9
         * ※內容描述  1.3.6.1.4.1.367.3.2.1.2.19.5.1.5
         * ========================================
         * 1.3.6.1.4.1.367.3.2.1.2.19.5.1.5.1 			Counter: Machine Total
         * 1.3.6.1.4.1.367.3.2.1.2.19.5.1.5.2			Counter:Copy:Total
         * 1.3.6.1.4.1.367.3.2.1.2.19.5.1.5.3			Counter:Copy:Black & White
         * 1.3.6.1.4.1.367.3.2.1.2.19.5.1.5.4			Counter:Copy:Single/Two-color
         * 1.3.6.1.4.1.367.3.2.1.2.19.5.1.5.5			Counter:Copy:Full Color
         * 1.3.6.1.4.1.367.3.2.1.2.19.5.1.5.6			Counter:FAX:Total
         * 1.3.6.1.4.1.367.3.2.1.2.19.5.1.5.7			Counter:FAX:Black & White
         * 1.3.6.1.4.1.367.3.2.1.2.19.5.1.5.8			Counter:Print:Total
         * 1.3.6.1.4.1.367.3.2.1.2.19.5.1.5.9			Counter:Print:Black & White
         * 1.3.6.1.4.1.367.3.2.1.2.19.5.1.5.10			Counter:Print:Single/Two-col.
         * 1.3.6.1.4.1.367.3.2.1.2.19.5.1.5.11			Counter:Print:Full Color
         * 1.3.6.1.4.1.367.3.2.1.2.19.5.1.5.12			Counter: Machine Total
         */
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> RICOH_MIBs(string ip) => SNMPv1_GetBulk(ip, "1.3.6.1.4.1.367.3.2.1.2.19.5.1.9", "RICOH_MIBs");
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> TOSHIBA_MIBs(string ip) => SNMPv1_GetBulk(ip, "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1", "TOSHIBA_MIBs");
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> CANON_MIBs(string ip) => SNMPv1_GetBulk(ip, "1.3.6.1.4.1.1602.1.11.1.3.1.4", "CANON_MIBs");
        private static Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> KYOCERA_MIBs(string ip) => SNMPv1_GetBulk(ip, "1.3.6.1.4.1.1347.42.3.1", "KYOCERA_MIBs");

        #endregion

        #endregion
    }
}

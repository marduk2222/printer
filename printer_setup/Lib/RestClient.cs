using DataClass;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Lib
{
    /// <summary>
    /// REST API Client for the printer web backend (ASP.NET Core)。
    /// ────────────────────────────────────────────────────────────
    /// 差異摘要（與舊版 CPIC / Odoo 版本比對）：
    ///   1. Authenticate 端點改為 /api/auth，無 session cookie；帳密驗證成功即回 true。
    ///   2. 所有 POST body 用 camelCase 序列化（CamelCasePropertyNamesContractResolver）。
    ///   3. /api/printer、/api/period 參數改 partnerId (string)；回傳 PrinterDataDto（modelId 為 int）。
    ///   4. /api/supplies 只接受扁平欄位（tonerBlack/…/drumYellow），CPIC 的 items 由 FlattenSupplies 壓平。
    ///   5. /api/alerts 只接受 int[]，Alert.code 非數字者會被丟棄。
    ///   6. /api/record 不再支援 state / data；state 改由 UpdateDevice 或 CreateInstall 傳遞。
    ///   7. UpdateDevice 改用 isActive (bool?) 控制啟用/停用；wire 欄位為 isActive。
    ///
    /// 已知限制：
    ///   - PrinterDataDto.modelId 為數字，PrinterScanner.Scan() 需要字串 model code（如 "ricoh"）。
    ///     Printer.model 目前直接填 modelId.ToString()，SNMP switch 將落到 default → 不做 Counter。
    ///     若需啟用 SNMP，後端 PrinterModel.Code 應設為 "common"/"ricoh"/"xerox"/"toshiba"/"kyocera"
    ///     且 PrinterDataDto 需要增設字串 model 欄位，或新增 /api/model/code 查詢端點。
    /// </summary>
    internal class RestClient
    {
        private static readonly JsonSerializerSettings CamelSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly HttpClient _client;
        private readonly string _baseUrl;
        private readonly Action<LogInfo> _trace;
        private readonly string _login;
        private readonly string _password;
        private bool _isAuthenticated;

        public RestClient(string baseUrl, Action<LogInfo> trace = null, string db = null, string login = null, string password = null)
        {
            _baseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
            _trace = trace;
            _login = login;
            _password = password;
            _client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _client.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public bool IsAuthenticated => _isAuthenticated;

        #region Auth / Ping

        public bool Authenticate()
        {
            if (string.IsNullOrEmpty(_login) || string.IsNullOrEmpty(_password))
            {
                Log("Authenticate", "skip", "auth", "No credentials configured");
                return false;
            }
            try
            {
                var json = JsonConvert.SerializeObject(new { account = _login, password = _password }, CamelSettings);
                var body = Post("/api/auth", json);
                if (body == null) return false;

                var response = Deserialize<ApiResponse<AuthResponse>>(body);
                _isAuthenticated = response != null && response.status == "success" && response.data != null && response.data.id > 0;
                Log("Authenticate", _isAuthenticated ? "success" : "failed", "auth",
                    _isAuthenticated ? $"uid={response.data.id}" : (response?.message ?? "invalid response"));
                return _isAuthenticated;
            }
            catch (Exception ex) { Log("Authenticate", "exception", "auth", ex.Message); return false; }
        }

        public bool Ping()
        {
            try
            {
                var body = Get("/api/ping");
                if (body == null) return false;
                var response = Deserialize<ApiResponse>(body);
                return response?.status == "success";
            }
            catch (Exception ex) { Log("Ping", "exception", "ping", ex.Message); return false; }
        }

        #endregion

        #region Companies / Printers

        /// <summary>POST /api/company — keyword 關鍵字搜尋</summary>
        public ApiResponse<List<CompanyItem>> GetCompanys(string keyword)
        {
            try
            {
                var json = JsonConvert.SerializeObject(new { keyword }, CamelSettings);
                var body = Post("/api/company", json);
                if (body == null) return null;
                return Deserialize<ApiResponse<List<CompanyItem>>>(body);
            }
            catch (Exception ex) { Log("GetCompanys", "exception", "companys", ex.Message); return null; }
        }

        /// <summary>POST /api/printer — 回傳該 partner 所有 isActive=true 的事務機</summary>
        public List<Printer> GetPrinter(int partnerId) => FetchPrinters("/api/printer", partnerId);

        /// <summary>POST /api/period — 只回傳 isActive=true 且同步計數器的事務機</summary>
        public List<Printer> GetPeriod(int partnerId) => FetchPrinters("/api/period", partnerId);

        private List<Printer> FetchPrinters(string endpoint, int partnerId)
        {
            try
            {
                var json = JsonConvert.SerializeObject(new { partnerId = partnerId.ToString() }, CamelSettings);
                var body = Post(endpoint, json);
                if (body == null) return null;

                var response = Deserialize<ApiResponse<List<PrinterDataDto>>>(body);
                if (response?.status != "success" || response.data == null) return null;

                var list = new List<Printer>();
                foreach (var p in response.data)
                {
                    list.Add(new Printer
                    {
                        id              = p.id,
                        code            = p.code,
                        name            = p.name ?? "",
                        mac             = p.mac ?? "",
                        ip              = p.ip ?? "",
                        printer_name    = p.printerName ?? "",
                        serial_number   = p.serialNumber ?? "",
                        model           = p.modelId > 0 ? p.modelId.ToString() : "",
                        is_active       = p.isActive,
                        printer_counter = false
                    });
                }
                return list;
            }
            catch (Exception ex) { Log("FetchPrinters", "exception", endpoint, ex.Message); return null; }
        }

        #endregion

        #region Device / Supplies / Alerts / Record / Install

        /// <summary>POST /api/device — 更新設備識別欄位 + isActive（啟用/停用）</summary>
        public bool UpdateDevice(string code, string mac = null, string ip = null, string serialNumber = null, string printerName = null, bool? isActive = null)
        {
            try
            {
                var json = JsonConvert.SerializeObject(new
                {
                    code,
                    mac,
                    ip,
                    serialNumber,
                    printerName,
                    isActive
                }, CamelSettings);
                var body = Post("/api/device", json);
                if (body == null) return false;
                var response = Deserialize<ApiResponse>(body);
                return response?.status == "success";
            }
            catch (Exception ex) { Log("UpdateDevice", "exception", "device", ex.Message); return false; }
        }

        /// <summary>POST /api/supplies — CPIC items[] 壓平成扁平欄位再送</summary>
        public bool UpdateSupplies(SuppliesRequest request)
        {
            try
            {
                var body = FlattenSupplies(request);
                var json = JsonConvert.SerializeObject(body, CamelSettings);
                var resp = Post("/api/supplies", json);
                if (resp == null) return false;
                var response = Deserialize<ApiResponse>(resp);
                return response?.status == "success";
            }
            catch (Exception ex) { Log("UpdateSupplies", "exception", "supplies", ex.Message); return false; }
        }

        /// <summary>POST /api/alerts — Alert.code 嘗試轉 int，失敗者捨棄</summary>
        public bool UpdateAlerts(AlertsRequest request)
        {
            try
            {
                var codes = new List<int>();
                if (request.alerts != null)
                {
                    foreach (var a in request.alerts)
                        if (int.TryParse(a.code, out var ci)) codes.Add(ci);
                }
                var json = JsonConvert.SerializeObject(new { code = request.code, alerts = codes }, CamelSettings);
                var body = Post("/api/alerts", json);
                if (body == null) return false;
                var response = Deserialize<ApiResponse>(body);
                return response?.status == "success";
            }
            catch (Exception ex) { Log("UpdateAlerts", "exception", "alerts", ex.Message); return false; }
        }

        /// <summary>POST /api/record — 只送 code / date / blackPrint / colorPrint / largePrint</summary>
        public bool WriteMeter(RecordRequest request)
        {
            try
            {
                var json = JsonConvert.SerializeObject(new
                {
                    code       = request.code,
                    date       = request.date,
                    blackPrint = request.black_print,
                    colorPrint = request.color_print,
                    largePrint = request.large_print
                }, CamelSettings);
                var body = Post("/api/record", json);
                if (body == null) return false;
                var response = Deserialize<ApiResponse>(body);
                return response?.status == "success";
            }
            catch (Exception ex) { Log("WriteMeter", "exception", "meter", ex.Message); return false; }
        }

        /// <summary>POST /api/install — 安裝/退機/換機事件紀錄（state "0"|"1"|"2"|"3"）</summary>
        public bool CreateInstall(string code, int state, string date = null)
        {
            try
            {
                var json = JsonConvert.SerializeObject(new
                {
                    code,
                    date  = date ?? DateTime.Now.ToString("yyyy-MM-dd"),
                    state = state.ToString()
                }, CamelSettings);
                var body = Post("/api/install", json);
                if (body == null) return false;
                var response = Deserialize<ApiResponse>(body);
                return response?.status == "success";
            }
            catch (Exception ex) { Log("CreateInstall", "exception", "install", ex.Message); return false; }
        }

        #endregion

        #region Version

        public UpdateResponse GetServiceVersion(string currentVersion)
        {
            try
            {
                var body = Get($"/api/service/version?version={currentVersion}");
                if (body == null) return null;
                var response = Deserialize<ApiResponse<UpdateResponse>>(body);
                return response?.data;
            }
            catch (Exception ex) { Log("GetServiceVersion", "exception", "version", ex.Message); return null; }
        }

        #endregion

        #region HTTP + helpers

        private string Post(string endpoint, string json)
        {
            var url = $"{_baseUrl}{endpoint}";
            Log("Post", "request", endpoint, json);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = _client.PostAsync(url, content).Result;
            var body = response.Content.ReadAsStringAsync().Result;
            Log("Post", response.IsSuccessStatusCode ? "response" : "error", endpoint, body);
            return response.IsSuccessStatusCode ? body : null;
        }

        private string Get(string endpoint)
        {
            var url = $"{_baseUrl}{endpoint}";
            Log("Get", "request", endpoint, url);
            var response = _client.GetAsync(url).Result;
            var body = response.Content.ReadAsStringAsync().Result;
            Log("Get", response.IsSuccessStatusCode ? "response" : "error", endpoint, body);
            return response.IsSuccessStatusCode ? body : null;
        }

        private T Deserialize<T>(string json)
        {
            if (string.IsNullOrEmpty(json)) return default;
            try { return JsonConvert.DeserializeObject<T>(json); }
            catch (Exception ex) { Log("Deserialize", "exception", typeof(T).Name, ex.Message); return default; }
        }

        private void Log(string function, string option, string identity, string message)
        {
            _trace?.Invoke(new LogInfo { File = "REST", Category = "REST", Function = function, Option = option, Identity = identity, Message = message });
        }

        private static object FlattenSupplies(SuppliesRequest req)
        {
            double? tBlk = req.toner_black, tCyn = req.toner_cyan, tMag = req.toner_magenta, tYlw = req.toner_yellow, tWst = req.toner_waste;
            double? dBlk = req.drum_black,  dCyn = req.drum_cyan,  dMag = req.drum_magenta,  dYlw = req.drum_yellow;
            if (req.items != null)
            {
                foreach (var s in req.items)
                {
                    if (s.type == "toner")
                    {
                        if      (s.color == "black")   tBlk = s.level;
                        else if (s.color == "cyan")    tCyn = s.level;
                        else if (s.color == "magenta") tMag = s.level;
                        else if (s.color == "yellow")  tYlw = s.level;
                        else if (s.color == "waste")   tWst = s.level;
                    }
                    else if (s.type == "drum")
                    {
                        if      (s.color == "black")   dBlk = s.level;
                        else if (s.color == "cyan")    dCyn = s.level;
                        else if (s.color == "magenta") dMag = s.level;
                        else if (s.color == "yellow")  dYlw = s.level;
                    }
                }
            }
            return new
            {
                code         = req.code,
                tonerBlack   = tBlk,
                tonerCyan    = tCyn,
                tonerMagenta = tMag,
                tonerYellow  = tYlw,
                tonerWaste   = tWst,
                drumBlack    = dBlk,
                drumCyan     = dCyn,
                drumMagenta  = dMag,
                drumYellow   = dYlw
            };
        }

        #endregion

        public void Dispose() { _client?.Dispose(); }
    }

    // ── printer web 回應 DTO（camelCase 一致） ───────────────────────
    internal class PrinterDataDto
    {
        public int id { get; set; }
        public string code { get; set; }
        public int? partnerId { get; set; }
        public int? userId { get; set; }
        public int modelId { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public bool isActive { get; set; }
        public string number { get; set; }
        public string serialNumber { get; set; }
        public string ip { get; set; }
        public string mac { get; set; }
        public string printerName { get; set; }
    }

    internal class AuthResponse
    {
        public int id { get; set; }
        public string name { get; set; }
        public string mobile { get; set; }
    }
}

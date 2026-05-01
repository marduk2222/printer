using DataClass;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Lib
{
    /// <summary>
    /// REST API Client for Odoo communication with session authentication
    /// </summary>
    internal class RestClient
    {
        private readonly HttpClientHandler _handler;
        private readonly HttpClient _client;
        private readonly CookieContainer _cookieContainer;
        private readonly string _baseUrl;
        private readonly Action<LogInfo> _trace;

        // Authentication credentials
        private readonly string _db;
        private readonly string _login;
        private readonly string _password;
        private bool _isAuthenticated = false;

        public RestClient(string baseUrl, Action<LogInfo> trace = null, string db = null, string login = null, string password = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _trace = trace;
            _db = db;
            _login = login;
            _password = password;

            // Setup HttpClient with CookieContainer for session management
            _cookieContainer = new CookieContainer();
            _handler = new HttpClientHandler
            {
                CookieContainer = _cookieContainer,
                UseCookies = true
            };
            _client = new HttpClient(_handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _client.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        #region Authentication
        /// <summary>
        /// Authenticate with Odoo using JSON-RPC session endpoint
        /// </summary>
        public bool Authenticate()
        {
            if (string.IsNullOrEmpty(_db) || string.IsNullOrEmpty(_login) || string.IsNullOrEmpty(_password))
            {
                Log("REST", "Authenticate", "skip", "auth", "No credentials configured");
                return false;
            }

            try
            {
                var payload = new
                {
                    jsonrpc = "2.0",
                    @params = new
                    {
                        db = _db,
                        login = _login,
                        password = _password
                    }
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var url = $"{_baseUrl}/web/session/authenticate";

                Log("REST", "Authenticate", "request", "auth", $"db={_db}, login={_login}");

                var response = _client.PostAsync(url, content).Result;
                var resultStr = response.Content.ReadAsStringAsync().Result;

                if (response.IsSuccessStatusCode)
                {
                    // Check if authentication was successful by parsing the response
                    var result = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(resultStr);
                    if (result?.result?.uid != null && (int)result.result.uid > 0)
                    {
                        _isAuthenticated = true;
                        Log("REST", "Authenticate", "success", "auth", $"uid={(int)result.result.uid}");
                        return true;
                    }
                    else
                    {
                        Log("REST", "Authenticate", "failed", "auth", "Invalid credentials or response");
                        return false;
                    }
                }
                else
                {
                    Log("REST", "Authenticate", "error", "auth", $"HTTP {(int)response.StatusCode}: {resultStr}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log("REST", "Authenticate", "exception", "auth", ex.Message);
                return false;
            }
        }

        // AuthenticateAsync — 未使用，以同步 Authenticate() + EnsureAuthenticated() 取代
        //public async Task<bool> AuthenticateAsync() { ... }

        /// <summary>
        /// Check if authenticated
        /// </summary>
        public bool IsAuthenticated => _isAuthenticated;

        /// <summary>
        /// Ensure authenticated before making API calls
        /// </summary>
        private bool EnsureAuthenticated()
        {
            if (_isAuthenticated) return true;
            return Authenticate();
        }
        #endregion

        #region Private Methods
        private void Log(string category, string function, string option, string identity, string message)
        {
            _trace?.Invoke(new LogInfo
            {
                File = "REST",
                Category = category,
                Function = function,
                Option = option,
                Identity = identity,
                Message = message
            });
        }

        private async Task<string> PostJsonAsync(string endpoint, string json, bool requireAuth = true)
        {
            try
            {
                // Ensure authenticated if required
                if (requireAuth && !EnsureAuthenticated())
                {
                    Log("REST", "PostJsonAsync", "auth_failed", endpoint, "Authentication required but failed");
                    // Continue anyway - some endpoints may not require auth
                }

                var url = $"{_baseUrl}{endpoint}";
                Log("REST", "PostJsonAsync", "request", endpoint, json);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _client.PostAsync(url, content).ConfigureAwait(false);
                var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                Log("REST", "PostJsonAsync", "response", endpoint, result);

                if (!response.IsSuccessStatusCode)
                {
                    Log("REST", "PostJsonAsync", "error", endpoint, $"HTTP {(int)response.StatusCode}: {result}");
                    return null;
                }

                return result;
            }
            catch (Exception ex)
            {
                Log("REST", "PostJsonAsync", "exception", endpoint, ex.Message);
                return null;
            }
        }

        private async Task<string> GetAsync(string endpoint, bool requireAuth = false)
        {
            try
            {
                // Ensure authenticated if required
                if (requireAuth && !EnsureAuthenticated())
                {
                    Log("REST", "GetAsync", "auth_failed", endpoint, "Authentication required but failed");
                }

                var url = $"{_baseUrl}{endpoint}";
                Log("REST", "GetAsync", "request", endpoint, url);

                var response = await _client.GetAsync(url).ConfigureAwait(false);
                var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                Log("REST", "GetAsync", "response", endpoint, result);

                if (!response.IsSuccessStatusCode)
                {
                    Log("REST", "GetAsync", "error", endpoint, $"HTTP {(int)response.StatusCode}: {result}");
                    return null;
                }

                return result;
            }
            catch (Exception ex)
            {
                Log("REST", "GetAsync", "exception", endpoint, ex.Message);
                return null;
            }
        }

        private T Deserialize<T>(string json)
        {
            if (string.IsNullOrEmpty(json)) return default;
            try
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception ex)
            {
                Log("REST", "Deserialize", "exception", typeof(T).Name, ex.Message);
                return default;
            }
        }
        #endregion

        #region Public API Methods

        /// <summary>
        /// Ping test for connection verification (needs auth for session)
        /// </summary>
        public bool Ping()
        {
            try
            {
                // Ensure authenticated first (for session cookie)
                EnsureAuthenticated();

                var result = GetAsync("/api/ping", requireAuth: false).Result;
                if (result == null) return false;

                // Response format: {"status": "success", "message": "pong", "data": {"db": "..."}}
                var response = Deserialize<ApiResponse>(result);
                return response?.status == "success";
            }
            catch (Exception ex)
            {
                Log("REST", "Ping", "exception", "ping", ex.Message);
                return false;
            }
        }

        // PingAsync — 未使用，以同步 Ping() 取代
        //public async Task<bool> PingAsync() { ... }

        // GetPrinters (/api/printer) — 未使用，主流程改用 GetPeriod (/api/period)
        //public List<Printer> GetPrinters(int companyId) { ... }

        /// <summary>
        /// Get period sync list for the company
        /// </summary>
        public List<Printer> GetPeriod(int companyId)
        {
            try
            {
                var request = new PeriodRequest { company_id = companyId };
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(request);
                var result = PostJsonAsync("/api/period", json).Result;
                if (result == null) return null;

                // Response format: {"status": "success", "message": "...", "data": [...]}
                var response = Deserialize<ApiResponse<List<PeriodItem>>>(result);
                if (response?.status != "success" || response.data == null) return null;

                // Convert REST response to Printer list
                var list = new List<Printer>();
                foreach (var item in response.data)
                {
                    list.Add(new Printer
                    {
                        id = item.id,
                        code = item.code,  // REST API primary identifier
                        name = item.name,
                        mac = item.mac,
                        ip = item.ip,
                        printer_name = item.printer_name,
                        serial_number = item.serial_number,
                        model = item.model,
                        state = item.state,
                        printer_counter = item.counter,
                        priority = item.priority,
                        date_start = item.date_start,
                        date_end = item.date_end
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                Log("REST", "GetPeriod", "exception", "period", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Update supplies information
        /// </summary>
        public bool UpdateSupplies(SuppliesRequest request)
        {
            try
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(request);
                var result = PostJsonAsync("/api/supplies", json).Result;
                if (result == null) return false;

                // Response format: {"status": "success"/false, "message": "..."}
                var response = Deserialize<ApiResponse>(result);
                return response?.status == "success";
            }
            catch (Exception ex)
            {
                Log("REST", "UpdateSupplies", "exception", "supplies", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Update alerts information
        /// </summary>
        public bool UpdateAlerts(AlertsRequest request)
        {
            try
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(request);
                var result = PostJsonAsync("/api/alerts", json).Result;
                if (result == null) return false;

                // Response format: {"status": "success"/false, "message": "..."}
                var response = Deserialize<ApiResponse>(result);
                return response?.status == "success";
            }
            catch (Exception ex)
            {
                Log("REST", "UpdateAlerts", "exception", "alerts", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Write meter reading record
        /// </summary>
        public bool WriteMeter(RecordRequest request)
        {
            try
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(request);
                var result = PostJsonAsync("/api/record", json).Result;
                if (result == null) return false;

                // Response format: {"status": "success"/false, "message": "..."}
                var response = Deserialize<ApiResponse>(result);
                return response?.status == "success";
            }
            catch (Exception ex)
            {
                Log("REST", "WriteMeter", "exception", "meter", ex.Message);
                return false;
            }
        }

        // UpdateSuppliesAsync — 未使用，以同步 UpdateSupplies() 取代
        //public async Task<bool> UpdateSuppliesAsync(SuppliesRequest request) { ... }

        // UpdateAlertsAsync — 未使用，以同步 UpdateAlerts() 取代
        //public async Task<bool> UpdateAlertsAsync(AlertsRequest request) { ... }

        // WriteRecordAsync — 未使用，以同步 WriteMeter() 取代
        //public async Task<bool> WriteRecordAsync(RecordRequest request) { ... }

        /// <summary>
        /// Update printer device information (mac, ip, serial_number, printer_name, state)
        /// POST /api/device
        /// </summary>
        public bool UpdateDevice(string code, string mac = null, string ip = null, string serialNumber = null, string printerName = null, int? state = null)
        {
            try
            {
                var request = new Dictionary<string, object> { { "code", code } };
                if (!string.IsNullOrEmpty(mac)) request["mac"] = mac;
                if (!string.IsNullOrEmpty(ip)) request["ip"] = ip;
                if (!string.IsNullOrEmpty(serialNumber)) request["serial_number"] = serialNumber;
                if (!string.IsNullOrEmpty(printerName)) request["printer_name"] = printerName;
                if (state.HasValue) request["state"] = state.Value;

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(request);
                var result = PostJsonAsync("/api/device", json).Result;
                if (result == null) return false;

                var response = Deserialize<ApiResponse>(result);
                return response?.status == "success";
            }
            catch (Exception ex)
            {
                Log("REST", "UpdateDevice", "exception", "device", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Get service version info from Odoo (with session authentication)
        /// </summary>
        public UpdateResponse GetServiceVersion(string currentVersion)
        {
            try
            {
                // Ensure authenticated first
                EnsureAuthenticated();

                var url = $"/api/service/version?version={currentVersion}";
                var result = GetAsync(url, requireAuth: false).Result;
                if (result == null) return null;

                Log("REST", "GetServiceVersion", "response", "version", result);

                return Deserialize<UpdateResponse>(result);
            }
            catch (Exception ex)
            {
                Log("REST", "GetServiceVersion", "exception", "version", ex.Message);
                return null;
            }
        }

        #endregion

        #region Helper Methods

        // ToSuppliesRequest — 未使用，SendSupplies 改為直接組 SuppliesRequest
        //public static SuppliesRequest ToSuppliesRequest(Printer printer) { ... }

        // ToAlertsRequest — 未使用，SendAlerts 改為直接組 AlertsRequest
        //public static AlertsRequest ToAlertsRequest(Printer printer) { ... }

        // AppendItems / ToMeterRequest — 已移除（舊架構：Sheets → PrintCount 多層中間結構）
        // 新架構由 Service1.Counter_*() 直接將 SNMP OID 對應為 CounterItem，
        // 並存入 Printer.items / Printer.snmp_data，不再需要此轉換方法。

        #endregion

        public void Dispose()
        {
            _client?.Dispose();
            _handler?.Dispose();
        }
    }
}

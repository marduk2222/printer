using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace printer_info
{
    /// <summary>
    /// Demo Test Program for Odoo Controller Verification
    /// 用於驗證 Odoo printer_cloud_meter 模組 API 對接
    /// </summary>
    public class DemoTest
    {
        private readonly HttpClientHandler _handler;
        private readonly HttpClient _client;
        private readonly CookieContainer _cookieContainer;
        private readonly string _baseUrl;
        private readonly string _db;
        private readonly string _login;
        private readonly string _password;
        private bool _isAuthenticated = false;

        public DemoTest(string baseUrl, string db = null, string login = null, string password = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _db = db;
            _login = login;
            _password = password;

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

        #region Main Entry
        public static void Run(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  Odoo Printer Controller Demo Test");
            Console.WriteLine("========================================\n");

            // 從 Config.ini 讀取設定
            var baseUrl = Lib.Setup.General.RestUrl;
            var db = Lib.Setup.General.RestDb;
            var login = Lib.Setup.General.RestLogin;
            var password = Lib.Setup.General.RestPassword;
            var partnerId = Lib.Setup.General.Partner;

            Console.WriteLine($"[Config]");
            Console.WriteLine($"  RestUrl  : {baseUrl}");
            Console.WriteLine($"  RestDb   : {db}");
            Console.WriteLine($"  RestLogin: {login}");
            Console.WriteLine($"  Partner  : {partnerId}");
            Console.WriteLine();

            var demo = new DemoTest(baseUrl, db, login, password);

            // 執行所有驗證測試
            demo.RunAllTests(partnerId, 0).GetAwaiter().GetResult();

            Console.WriteLine("\n========================================");
            Console.WriteLine("  Demo Test Completed");
            Console.WriteLine("========================================");

            // 僅在互動模式下等待按鍵
            try { Console.ReadKey(); } catch { }
        }
        #endregion

        #region Test Runner
        public async Task RunAllTests(int companyId, int placeId)
        {
            // Test 1: Authentication (先取得 session)
            await TestAuthentication();

            // Test 2: Ping
            await TestPing();

            // Test 3: Get Printers
            await TestGetPrinters(companyId);

            // Test 4: Get Period
            await TestGetPeriod(companyId);

            // Test 5: Update Supplies
            await TestUpdateSupplies();

            // Test 6: Update Alerts
            await TestUpdateAlerts();

            // Test 7: Write Meter
            await TestWriteMeter();
        }
        #endregion

        #region Test Methods
        /// <summary>
        /// Test 1: Ping - 測試連線 (GET)
        /// </summary>
        private async Task TestPing()
        {
            PrintTestHeader("1. Ping Test", "/api/ping [GET]");
            try
            {
                var url = $"{_baseUrl}/api/ping";

                PrintRequest("GET");
                var response = await _client.GetAsync(url);
                var result = await response.Content.ReadAsStringAsync();
                PrintResponse(result);

                if (response.IsSuccessStatusCode)
                {
                    var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(result);
                    var status = (string)parsed?.status;
                    var message = (string)parsed?.message;
                    PrintResult(status == "success", $"status={status}, message={message}");
                }
                else
                {
                    PrintResult(false, $"HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                PrintError(ex.Message);
            }
        }

        /// <summary>
        /// Test 2: Authentication - 測試 Odoo Session 認證
        /// </summary>
        private async Task TestAuthentication()
        {
            PrintTestHeader("2. Authentication Test", "/web/session/authenticate");
            try
            {
                if (string.IsNullOrEmpty(_db) || string.IsNullOrEmpty(_login))
                {
                    PrintSkip("No credentials configured");
                    return;
                }

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

                PrintRequest($"{{db={_db}, login={_login}, password=***}}");
                var response = await _client.PostAsync(url, content);
                var result = await response.Content.ReadAsStringAsync();
                PrintResponse(result);

                if (response.IsSuccessStatusCode)
                {
                    var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(result);
                    if (parsed?.result?.uid != null && (int)parsed.result.uid > 0)
                    {
                        _isAuthenticated = true;
                        PrintResult(true, $"Authenticated! uid={(int)parsed.result.uid}");
                    }
                    else
                    {
                        PrintResult(false, "Invalid credentials");
                    }
                }
                else
                {
                    PrintResult(false, $"HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                PrintError(ex.Message);
            }
        }

        /// <summary>
        /// Test 3: Get Printers - 取得印表機列表
        /// </summary>
        private async Task TestGetPrinters(int companyId)
        {
            PrintTestHeader("3. Get Printers Test", "/api/printer");
            try
            {
                var payload = new { company_id = companyId };
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var url = $"{_baseUrl}/api/printer";

                PrintRequest(json);
                var response = await _client.PostAsync(url, content);
                var result = await response.Content.ReadAsStringAsync();
                PrintResponse(result);

                if (response.IsSuccessStatusCode)
                {
                    var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(result);
                    var status = (string)parsed?.status;
                    var message = (string)parsed?.message;
                    var printers = parsed?.data as Newtonsoft.Json.Linq.JArray;
                    var count = printers?.Count ?? 0;
                    PrintResult(status == "success", $"status={status}, message={message}");

                    if (printers != null)
                    {
                        int i = 0;
                        foreach (var p in printers)
                        {
                            if (i >= 3) { Console.WriteLine("    ..."); break; }
                            Console.WriteLine($"    [{i}] code={p["code"]}, name={p["name"]}, ip={p["ip"]}");
                            i++;
                        }
                    }
                }
                else
                {
                    PrintResult(false, $"HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                PrintError(ex.Message);
            }
        }

        /// <summary>
        /// Test 4: Get Period - 取得排程印表機列表
        /// </summary>
        private async Task TestGetPeriod(int companyId)
        {
            PrintTestHeader("4. Get Period Test", "/api/period");
            try
            {
                var payload = new { company_id = companyId };
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var url = $"{_baseUrl}/api/period";

                PrintRequest(json);
                var response = await _client.PostAsync(url, content);
                var result = await response.Content.ReadAsStringAsync();
                PrintResponse(result);

                if (response.IsSuccessStatusCode)
                {
                    var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(result);
                    var status = (string)parsed?.status;
                    var message = (string)parsed?.message;
                    var printers = parsed?.data as Newtonsoft.Json.Linq.JArray;
                    var count = printers?.Count ?? 0;
                    PrintResult(status == "success", $"status={status}, message={message}");

                    if (printers != null)
                    {
                        int i = 0;
                        foreach (var p in printers)
                        {
                            if (i >= 3) { Console.WriteLine("    ..."); break; }
                            Console.WriteLine($"    [{i}] code={p["code"]}, name={p["name"]}, ip={p["ip"]}, model_id={p["model_id"]}");
                            i++;
                        }
                    }
                }
                else
                {
                    PrintResult(false, $"HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                PrintError(ex.Message);
            }
        }

        /// <summary>
        /// Test 5: Update Supplies - 更新碳粉/滾筒資訊
        /// </summary>
        private async Task TestUpdateSupplies()
        {
            PrintTestHeader("5. Update Supplies Test", "/api/supplies");
            try
            {
                var payload = new
                {
                    code = "1",
                    toner_black = 0.75,
                    toner_cyan = 0.60,
                    toner_magenta = 0.55,
                    toner_yellow = 0.80,
                    drum_black = 0.90
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var url = $"{_baseUrl}/api/supplies";

                PrintRequest(json);
                var response = await _client.PostAsync(url, content);
                var result = await response.Content.ReadAsStringAsync();
                PrintResponse(result);

                if (response.IsSuccessStatusCode)
                {
                    var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(result);
                    var status = (string)parsed?.status;
                    var message = (string)parsed?.message;
                    PrintResult(status == "success", $"status={status}, message={message}");
                }
                else
                {
                    PrintResult(false, $"HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                PrintError(ex.Message);
            }
        }

        /// <summary>
        /// Test 6: Update Alerts - 更新告警資訊
        /// </summary>
        private async Task TestUpdateAlerts()
        {
            PrintTestHeader("6. Update Alerts Test", "/api/alerts");
            try
            {
                var payload = new
                {
                    code = "1",
                    alerts = new[]
                    {
                        new { code = "8", description = "Low Toner",  time = "" },
                        new { code = "4", description = "Paper Jam",  time = "" }
                    }
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var url = $"{_baseUrl}/api/alerts";

                PrintRequest(json);
                var response = await _client.PostAsync(url, content);
                var result = await response.Content.ReadAsStringAsync();
                PrintResponse(result);

                if (response.IsSuccessStatusCode)
                {
                    var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(result);
                    var status = (string)parsed?.status;
                    var message = (string)parsed?.message;
                    PrintResult(status == "success", $"status={status}, message={message}");
                }
                else
                {
                    PrintResult(false, $"HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                PrintError(ex.Message);
            }
        }

        /// <summary>
        /// Test 7: Write Meter - 寫入抄表記錄
        /// </summary>
        private async Task TestWriteMeter()
        {
            PrintTestHeader("7. Write Meter Test", "/api/record");
            try
            {
                var payload = new
                {
                    code = "1",
                    date = DateTime.Now.ToString("yyyy-MM-dd"),
                    items = new[]
                    {
                        new { job_type = "total", color = "black",      size = "normal", sheets = 12345 },
                        new { job_type = "total", color = "color_full", size = "normal", sheets = 6789  },
                        new { job_type = "total", color = "black",      size = "large",  sheets = 100   }
                    }
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var url = $"{_baseUrl}/api/record";

                PrintRequest(json);
                var response = await _client.PostAsync(url, content);
                var result = await response.Content.ReadAsStringAsync();
                PrintResponse(result);

                if (response.IsSuccessStatusCode)
                {
                    var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(result);
                    var status = (string)parsed?.status;
                    var message = (string)parsed?.message;
                    PrintResult(status == "success", $"status={status}, message={message}");
                }
                else
                {
                    PrintResult(false, $"HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                PrintError(ex.Message);
            }
        }
        #endregion

        #region Helper Methods
        private void PrintTestHeader(string testName, string endpoint)
        {
            Console.WriteLine($"\n----------------------------------------");
            Console.WriteLine($"[{testName}]");
            Console.WriteLine($"  Endpoint: {endpoint}");
        }

        private void PrintRequest(string json)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  >> Request:  {json}");
            Console.ResetColor();
        }

        private void PrintResponse(string json)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            // 截斷過長的回應
            var display = json.Length > 500 ? json.Substring(0, 500) + "..." : json;
            Console.WriteLine($"  << Response: {display}");
            Console.ResetColor();
        }

        private void PrintResult(bool success, string message)
        {
            var status = success ? "OK" : "FAIL";
            var color = success ? ConsoleColor.Green : ConsoleColor.Red;
            Console.ForegroundColor = color;
            Console.WriteLine($"  Result: [{status}] {message}");
            Console.ResetColor();
        }

        private void PrintError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  Error: {message}");
            Console.ResetColor();
        }

        private void PrintSkip(string reason)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Skipped: {reason}");
            Console.ResetColor();
        }
        #endregion
    }
}

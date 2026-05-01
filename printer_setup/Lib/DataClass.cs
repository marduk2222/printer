using System.Collections.Generic;

namespace DataClass
{
    // ──────────────────────────────────────────────────────────────────────────
    // System / Log
    // ──────────────────────────────────────────────────────────────────────────
    #region System
    internal class LogInfo
    {
        public string Timestamp { get; set; }   // 日期時間
        public string File      { get; set; }   // 檔案（Log / Error / REST）
        public string Module    { get; set; }   // 程序模組
        public string Category  { get; set; }   // 程序種類
        public string Function  { get; set; }   // 程序 Function
        public string Identity  { get; set; }   // 識別（printer code 等）
        public string Option    { get; set; }   // 狀態（request / success / failed / exception）
        public string Message   { get; set; }   // 訊息或錯誤
    }

    internal class LogFile
    {
        public string File { get; set; }
        public System.IO.StreamWriter StreamWriter { get; set; }

        public void Clear()
        {
            File = null;
            if (StreamWriter != null) { StreamWriter.Close(); StreamWriter.Dispose(); StreamWriter = null; }
        }
    }
    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    // SNMP Counter 結構
    // OID 直接映射為 CounterItem（job_type / color / size / sheets），
    // 不再經過 Counter → Sheets → PrintCount 多層中間結構。
    // ──────────────────────────────────────────────────────────────────────────

    // ──────────────────────────────────────────────────────────────────────────
    // Supplies（碳粉 / 感光鼓）
    // OID 直接映射為 SupplyItem（type / color / level），
    // 不再經過 Supplies → CMYK 多層中間結構。
    // ──────────────────────────────────────────────────────────────────────────

    // ──────────────────────────────────────────────────────────────────────────
    // Printer（事務機）
    // ──────────────────────────────────────────────────────────────────────────
    #region Printer

    public class Alert
    {
        public int    index       { get; set; }
        public string code        { get; set; }
        public string time        { get; set; }
        public string description { get; set; }
    }

    /// <summary>
    /// 事務機執行期間的完整資料模型。
    ///
    /// 設計原則：
    ///   - 識別欄位由 /api/period 回傳並填入
    ///   - items / snmp_data / supplies / alerts 由 SNMP 抓取後填入
    ///   - counter 維度直接由 OID 映射為 CounterItem list（items），
    ///     不再經過 Counter → Sheets → PrintCount 多層中間結構
    ///
    /// is_active: true=啟用中, false=停用 / 退機
    /// </summary>
    public class Printer
    {
        // ── 識別資訊（由 /api/period 填入）──────────────────────────────────
        public int    id            { get; set; }   // Odoo record id
        public string code          { get; set; }   // 主要識別碼（REST API key）
        public string name          { get; set; }   // 顯示名稱
        public string printer_name  { get; set; }   // 型號名稱
        public string serial_number { get; set; }   // 序號
        public string mac           { get; set; }   // MAC
        public string ip            { get; set; }   // IP
        public string model         { get; set; }   // 型號代碼（common/ricoh/ricoh_imc/xerox/toshiba/kyocera）
        public bool   is_active     { get; set; }   // 是否啟用（true=使用中,false=停用）

        // ── 排程設定（由 /api/period 填入）─────────────────────────────────
        public bool   printer_counter { get; set; }   // 是否啟用計數器抄表
        public int    priority        { get; set; }
        public string date_start      { get; set; }
        public string date_end        { get; set; }

        // ── SNMP 抓取資料（由 Printer() 方法填入）──────────────────────────
        public List<CounterItem> items        { get; set; }   // 計數器維度清單（OID 直接映射）
        public string            snmp_data    { get; set; }   // 計數器 SNMP 原始 JSON（除錯用）
        public List<SupplyItem>  supply_items { get; set; }   // 耗材維度清單（OID 直接映射，level=百分比）
        public List<Alert>       alerts       { get; set; }   // 告警清單

        public Printer()
        {
            items        = new List<CounterItem>();
            supply_items = new List<SupplyItem>();
            alerts       = new List<Alert>();
        }
    }
    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    // REST API 請求 / 回應
    // ──────────────────────────────────────────────────────────────────────────
    #region REST API

    // ── 通用回應 ─────────────────────────────────────────────────────────────
    public class ApiResponse
    {
        public string status  { get; set; }
        public string message { get; set; }
    }

    public class ApiResponse<T>
    {
        public string status  { get; set; }
        public string message { get; set; }
        public T      data    { get; set; }
    }

    // ── /api/company 請求 / 回應（printer_client UI 用） ───────────────────────
    public class CompanyRequest
    {
        public string keyword { get; set; }
    }

    public class CompanyItem
    {
        public int    id   { get; set; }   // 對應 Odoo res.partner.id，可直接作為 Partner ID 使用
        public string code { get; set; }
        public string name { get; set; }
    }

    // ── /api/period 請求 / 回應 ───────────────────────────────────────────────
    public class PeriodRequest
    {
        public int company_id { get; set; }
    }

    public class PeriodItem
    {
        public string code          { get; set; }   // 主要識別碼
        public string company_id    { get; set; }
        public string number        { get; set; }   // Partner number
        public string model         { get; set; }   // 型號代碼（common/ricoh/ricoh_imc/xerox/toshiba/kyocera）
        public string name          { get; set; }
        public string description   { get; set; }
        public string printer_name  { get; set; }
        public string serial_number { get; set; }
        public string ip            { get; set; }
        public string mac           { get; set; }
        public int    id            { get; set; }
        public bool   counter       { get; set; }   // printer_counter
        public int    priority      { get; set; }
        public string date_start    { get; set; }
        public string date_end      { get; set; }
        public int state { get; set; }   // 0=正常, 1=新裝機, 2=退機, 3=換機（對應 Odoo printer.data.state）
    }

    // ── /api/supplies 請求 ────────────────────────────────────────────────────

    /// <summary>
    /// 耗材明細項目（新維度格式）
    /// type  : toner / drum
    /// color : black / cyan / magenta / yellow / waste
    /// level : 剩餘比例（0.0 ~ 1.0，由 level/capacity 計算）
    /// </summary>
    public class SupplyItem
    {
        public string type  { get; set; }   // toner / drum
        public string color { get; set; }   // black / cyan / magenta / yellow / waste
        public double level { get; set; }   // 0.0 ~ 1.0
    }

    /// <summary>
    /// 耗材更新請求（新格式）
    /// items 為 SupplyItem list，每筆包含 type / color / level。
    /// 舊格式欄位（toner_black 等）保留供向下相容，items 有值時不送。
    /// </summary>
    public class SuppliesRequest
    {
        public string code { get; set; }

        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public List<SupplyItem> items { get; set; }

        // 舊格式（向下相容）
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? toner_black   { get; set; }
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? toner_cyan    { get; set; }
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? toner_magenta { get; set; }
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? toner_yellow  { get; set; }
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? toner_waste   { get; set; }
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? drum_black    { get; set; }
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? drum_cyan     { get; set; }
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? drum_magenta  { get; set; }
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? drum_yellow   { get; set; }
    }

    // ── /api/alerts 請求 ─────────────────────────────────────────────────────

    /// <summary>
    /// 告警更新請求
    /// alerts 為 Alert list，每筆包含 code / description / time。
    /// code        : SNMP hrPrinterDetectedErrorState 代碼
    /// description : SNMP 回傳的錯誤描述
    /// time        : SNMP 回傳的時間（uptime ticks 或時間戳記字串）
    /// </summary>
    public class AlertsRequest
    {
        public string       code   { get; set; }
        public List<Alert>  alerts { get; set; }
    }

    // ── /api/record 請求 ─────────────────────────────────────────────────────

    /// <summary>
    /// 計數器明細項目（新維度格式）
    /// job_type : print / copy / fax / scan / total
    /// color    : black / color_full / mono / duotone
    /// size     : normal / large
    /// </summary>
    public class CounterItem
    {
        public string job_type { get; set; }
        public string color    { get; set; }
        public string size     { get; set; }
        public int    sheets   { get; set; }
    }

    /// <summary>
    /// 抄表紀錄請求（新格式）
    /// items 為 CounterItem list，包含完整的 job_type / color / size / sheets 維度。
    /// data  為 SNMP 抓回的原始 JSON，供 Odoo 端除錯用。
    /// 舊格式欄位（black_print 等）保留供向下相容，items 有值時不送。
    ///
    /// state: 0=一般, 1=起表(新裝機), 2=尾表(退機), 3=換機
    /// </summary>
    public class RecordRequest
    {
        public string code  { get; set; }
        public string date  { get; set; }   // yyyy-MM-dd
        public int    state { get; set; }   // 0=一般, 1=起表, 2=尾表, 3=換機

        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string data  { get; set; }   // SNMP 原始 JSON（來自 Printer.snmp_data，除錯用）

        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public List<CounterItem> items { get; set; }

        // 舊格式（向下相容）
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? black_print { get; set; }
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? color_print { get; set; }
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? large_print { get; set; }
    }
    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    // 已停用 / 舊協定（保留紀錄）
    // ──────────────────────────────────────────────────────────────────────────
    //
    // Transfer      — 舊 Socket 協定，已改 REST API
    // SQLInfo       — SQL 查詢輔助，已無 DB 直連
    // SvcRequest    — 舊 Svc 控制協定
    // SvcResponse   — 同上
    // PrinterRequest  — /api/printer，主流程改用 /api/period
    // PrinterResponse — 同上
    // PeriodResponse  — 已改用 ApiResponse<List<PeriodItem>> 解析
    // PingResponse    — 舊 Ping 格式
    // LegacyApiResponse / LegacyApiResult — 舊回應格式
    // ErrorResponse   — 舊錯誤格式
    // FileInfo / DirInfo — 舊檔案管理（Socket 傳輸用）
}

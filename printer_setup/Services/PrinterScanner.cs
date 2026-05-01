using System;
using System.Collections.Generic;
using System.Net;
using DataClass;
using Lib;
using Newtonsoft.Json;
using printer_setup.Infrastructure;
using SnmpSharpNet;

namespace printer_setup.Services
{
    /// <summary>
    /// 事務機 SNMP 抓取服務。
    /// 內容完全對齊原 MainWindow.xaml.cs 的 Printer / Counter_* / Supplies* / SNMP* 區塊，
    /// 將 UI 無關的 SNMP + 品牌分派邏輯從 View 中分離出來。
    ///
    /// 使用流程：
    ///   1. Scan(printer) → 以 printer.ip / printer.model 為輸入，填入 items / supply_items / alerts / snmp_data
    ///   2. 呼叫方可視需要再呼叫 RestClient.UpdateDevice 更新 Odoo 資料
    /// </summary>
    internal class PrinterScanner
    {
        private readonly AsyncLogger _logger;
        private readonly RestClient _rest;

        public PrinterScanner(AsyncLogger logger, RestClient rest)
        {
            _logger = logger;
            _rest = rest;
        }

        private void Log(LogInfo info) => _logger?.Write(info);

        /// <summary>
        /// 對單一事務機執行 SNMP 抓取。成功回傳 true，失敗或無 IP 回傳 false。
        /// 原本 Printer() 方法使用「true = 失敗」的反向語意，這裡正規化為「true = 成功」。
        /// </summary>
        public bool Scan(Printer mfp)
        {
            try
            {
                var ip = mfp.ip;
                if (string.IsNullOrEmpty(ip)) return false;
                Log(new LogInfo { Category = "SNMP", Function = "Scan", Option = "ready", Message = $"ip:{ip}/model:{mfp.model}/printer_counter:{mfp.printer_counter}" });

                var macs         = Printer_MAC(ip);
                var printNames   = Printer_PrinterName(ip);
                var serialNames  = Printer_SerialNumber(ip);

                var firstMac    = FirstValue(macs);
                var firstPName  = FirstValue(printNames);
                var firstSerial = FirstValue(serialNames);

                if (string.IsNullOrEmpty(mfp.mac) || string.IsNullOrEmpty(mfp.printer_name) || string.IsNullOrEmpty(mfp.serial_number))
                {
                    mfp.mac           = firstMac;
                    mfp.printer_name  = firstPName;
                    mfp.serial_number = firstSerial;

                    if (_rest != null && !string.IsNullOrEmpty(mfp.code))
                    {
                        var ok = _rest.UpdateDevice(
                            code: mfp.code,
                            mac: mfp.mac,
                            ip: mfp.ip,
                            serialNumber: mfp.serial_number,
                            printerName: mfp.printer_name,
                            isActive: true);
                        Log(new LogInfo { Category = "REST", Function = "Scan", Option = "UpdateDevice", Message = $"code={mfp.code}, success={ok}" });
                    }
                }

                if (!RunCounter(mfp)) return false;
                ReadAlerts(ip, mfp);
                return true;
            }
            catch (Exception ex)
            {
                Log(new LogInfo { File = "Error", Category = "SNMP", Function = "Scan", Option = "exception", Message = ex.Message });
                return false;
            }
            finally { System.Threading.Thread.Sleep(1000); }
        }

        private bool RunCounter(Printer mfp)
        {
            switch (mfp.model)
            {
                case "common":    return Counter_Common(mfp);
                case "ricoh":
                case "ricoh_imc": return Counter_Ricoh(mfp);
                case "xerox":     return Counter_Xerox(mfp);
                case "toshiba":   return Counter_Toshiba(mfp);
                case "kyocera":   return Counter_Kyocera(mfp);
                default:          return true;
            }
        }

        private void ReadAlerts(string ip, Printer mfp)
        {
            var alerts = Printer_Alert(ip);
            var alertMap = new Dictionary<string, Alert>();
            const string pfx7 = "1.3.6.1.2.1.43.18.1.1.7.";
            const string pfx8 = "1.3.6.1.2.1.43.18.1.1.8.";
            const string pfx9 = "1.3.6.1.2.1.43.18.1.1.9.";
            foreach (var obj in alerts)
            {
                var oid = obj.Key.ToString();
                var val = obj.Value.ToString();
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
            foreach (var kvp in alertMap) mfp.alerts.Add(kvp.Value);
        }

        private static string FirstValue(Dictionary<Oid, AsnType> dict)
        {
            foreach (var kvp in dict) return kvp.Value?.ToString() ?? "";
            return "";
        }

        // ── Counter_* ─────────────────────────────────────────────────────────

        private bool Counter_Common(Printer mfp)
        {
            var ip = mfp.ip;
            Log(new LogInfo { Category = "SNMP", Function = "Counter_Common", Option = "request", Identity = ip });
            var counter = COMMON_MIBs(ip);
            Log(new LogInfo { Category = "SNMP", Function = "Counter_Common", Option = "response", Identity = ip, Message = JsonConvert.SerializeObject(counter) });
            if (counter == null) return false;

            int totalBlack = 0;
            foreach (var obj in counter)
                if (obj.Key.ToString() == "1.3.6.1.2.1.43.10.2.1.4.1.1")
                    int.TryParse(obj.Value.ToString(), out totalBlack);

            mfp.snmp_data = JsonConvert.SerializeObject(counter);
            if (totalBlack > 0) mfp.items.Add(new CounterItem { job_type = "print", color = "black", size = "normal", sheets = totalBlack });
            return Supplies(mfp);
        }

        /// <summary>
        /// Ricoh 標準系列（C2800 / C3300 / C3500 / C3505 / C4500 / C5505 等）
        /// </summary>
        private bool Counter_Ricoh(Printer mfp)
        {
            var ip = mfp.ip;
            Log(new LogInfo { Category = "SNMP", Function = "Counter_Ricoh", Option = "request", Identity = ip });
            var counter = RICOH_MIBs(ip);
            Log(new LogInfo { Category = "SNMP", Function = "Counter_Ricoh", Option = "response", Identity = ip, Message = JsonConvert.SerializeObject(counter) });
            if (counter == null) return false;

            int copyBlack = 0, copyDuotone = 0, copyColor = 0;
            int faxBlack = 0;
            int printBlack = 0, printDuotone = 0, printColor = 0;

            foreach (var obj in counter)
                switch (obj.Key.ToString())
                {
                    case "1.3.6.1.4.1.367.3.2.1.2.19.5.1.9.3":  int.TryParse(obj.Value.ToString(), out copyBlack);    break;
                    case "1.3.6.1.4.1.367.3.2.1.2.19.5.1.9.4":  int.TryParse(obj.Value.ToString(), out copyDuotone);  break;
                    case "1.3.6.1.4.1.367.3.2.1.2.19.5.1.9.5":  int.TryParse(obj.Value.ToString(), out copyColor);    break;
                    case "1.3.6.1.4.1.367.3.2.1.2.19.5.1.9.7":  int.TryParse(obj.Value.ToString(), out faxBlack);     break;
                    case "1.3.6.1.4.1.367.3.2.1.2.19.5.1.9.9":  int.TryParse(obj.Value.ToString(), out printBlack);   break;
                    case "1.3.6.1.4.1.367.3.2.1.2.19.5.1.9.10": int.TryParse(obj.Value.ToString(), out printDuotone); break;
                    case "1.3.6.1.4.1.367.3.2.1.2.19.5.1.9.11": int.TryParse(obj.Value.ToString(), out printColor);   break;
                }
            mfp.snmp_data = JsonConvert.SerializeObject(counter);

            if (copyBlack    > 0) mfp.items.Add(new CounterItem { job_type = "copy",  color = "black",      size = "normal", sheets = copyBlack });
            if (copyDuotone  > 0) mfp.items.Add(new CounterItem { job_type = "copy",  color = "duotone",    size = "normal", sheets = copyDuotone });
            if (copyColor    > 0) mfp.items.Add(new CounterItem { job_type = "copy",  color = "color_full", size = "normal", sheets = copyColor + copyDuotone });
            if (faxBlack     > 0) mfp.items.Add(new CounterItem { job_type = "fax",   color = "black",      size = "normal", sheets = faxBlack });
            if (printBlack   > 0) mfp.items.Add(new CounterItem { job_type = "print", color = "black",      size = "normal", sheets = printBlack });
            if (printDuotone > 0) mfp.items.Add(new CounterItem { job_type = "print", color = "duotone",    size = "normal", sheets = printDuotone });
            if (printColor   > 0) mfp.items.Add(new CounterItem { job_type = "print", color = "color_full", size = "normal", sheets = printColor + printDuotone });

            return Supplies(mfp);
        }

        private bool Counter_Xerox(Printer mfp)
        {
            var ip = mfp.ip;
            Log(new LogInfo { Category = "SNMP", Function = "Counter_Xerox", Option = "request", Identity = ip });
            var counter = XEROX_MIBs(ip);
            Log(new LogInfo { Category = "SNMP", Function = "Counter_Xerox", Option = "response", Identity = ip, Message = JsonConvert.SerializeObject(counter) });
            if (counter == null) return false;

            int printBlack = 0, printBlackLarge = 0;
            int printColor = 0, printColorLarge = 0;
            int faxBlack = 0;

            foreach (var obj in counter)
                switch (obj.Key.ToString())
                {
                    case "1.3.6.1.4.1.253.8.53.13.2.1.6.1.20.7":  int.TryParse(obj.Value.ToString(), out printBlack);       break;
                    case "1.3.6.1.4.1.253.8.53.13.2.1.6.1.20.10": int.TryParse(obj.Value.ToString(), out printBlackLarge);  break;
                    case "1.3.6.1.4.1.253.8.53.13.2.1.6.1.20.29": int.TryParse(obj.Value.ToString(), out printColor);       break;
                    case "1.3.6.1.4.1.253.8.53.13.2.1.6.1.20.32": int.TryParse(obj.Value.ToString(), out printColorLarge);  break;
                    case "1.3.6.1.4.1.253.8.53.13.2.1.6.1.20.71": int.TryParse(obj.Value.ToString(), out faxBlack);         break;
                }
            mfp.snmp_data = JsonConvert.SerializeObject(counter);

            if (printBlack      > 0) mfp.items.Add(new CounterItem { job_type = "print", color = "black",      size = "normal", sheets = printBlack });
            if (printBlackLarge > 0) mfp.items.Add(new CounterItem { job_type = "print", color = "black",      size = "large",  sheets = printBlackLarge });
            if (printColor      > 0) mfp.items.Add(new CounterItem { job_type = "print", color = "color_full", size = "normal", sheets = printColor });
            if (printColorLarge > 0) mfp.items.Add(new CounterItem { job_type = "print", color = "color_full", size = "large",  sheets = printColorLarge });
            if (faxBlack        > 0) mfp.items.Add(new CounterItem { job_type = "fax",   color = "black",      size = "normal", sheets = faxBlack });

            return Supplies(mfp);
        }

        private bool Counter_Toshiba(Printer mfp)
        {
            var ip = mfp.ip;
            Log(new LogInfo { Category = "SNMP", Function = "Counter_Toshiba", Option = "request", Identity = ip });
            var counter = TOSHIBA_MIBs(ip);
            Log(new LogInfo { Category = "SNMP", Function = "Counter_Toshiba", Option = "response", Identity = ip, Message = JsonConvert.SerializeObject(counter) });
            if (counter == null) return false;

            int printBlack = 0, printColor = 0, printDuotone = 0;
            int printBlackLarge = 0, printColorLarge = 0, printDuotoneLarge = 0;
            int scanBlack = 0, scanColor = 0, scanDuotone = 0;
            int scanBlackLarge = 0, scanColorLarge = 0, scanDuotoneLarge = 0;

            int v;
            foreach (var obj in counter)
                switch (obj.Key.ToString())
                {
                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.2.1.3":  if (int.TryParse(obj.Value.ToString(), out v)) printBlack += v; break;

                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.207.1.1": if (int.TryParse(obj.Value.ToString(), out v)) printColorLarge   += v; break;
                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.207.1.2": if (int.TryParse(obj.Value.ToString(), out v)) printDuotoneLarge += v; break;

                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.208.1.1": if (int.TryParse(obj.Value.ToString(), out v)) printColor   += v; break;
                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.208.1.2": if (int.TryParse(obj.Value.ToString(), out v)) printDuotone += v; break;

                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.8.1.3":   if (int.TryParse(obj.Value.ToString(), out v)) scanBlack += v; break;
                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.9.1.3":   if (int.TryParse(obj.Value.ToString(), out v)) scanBlack += v; break;
                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.10.1.3":  if (int.TryParse(obj.Value.ToString(), out v)) scanBlack += v; break;

                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.220.1.1": if (int.TryParse(obj.Value.ToString(), out v)) scanColor   += v; break;
                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.220.1.2": if (int.TryParse(obj.Value.ToString(), out v)) scanDuotone += v; break;
                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.222.1.1": if (int.TryParse(obj.Value.ToString(), out v)) scanColor   += v; break;
                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.222.1.2": if (int.TryParse(obj.Value.ToString(), out v)) scanDuotone += v; break;
                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.224.1.1": if (int.TryParse(obj.Value.ToString(), out v)) scanColor   += v; break;
                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.224.1.2": if (int.TryParse(obj.Value.ToString(), out v)) scanDuotone += v; break;

                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.219.1.1": if (int.TryParse(obj.Value.ToString(), out v)) scanColorLarge   += v; break;
                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.219.1.2": if (int.TryParse(obj.Value.ToString(), out v)) scanDuotoneLarge += v; break;
                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.221.1.1": if (int.TryParse(obj.Value.ToString(), out v)) scanColorLarge   += v; break;
                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.221.1.2": if (int.TryParse(obj.Value.ToString(), out v)) scanDuotoneLarge += v; break;
                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.223.1.1": if (int.TryParse(obj.Value.ToString(), out v)) scanColorLarge   += v; break;
                    case "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1.223.1.2": if (int.TryParse(obj.Value.ToString(), out v)) scanDuotoneLarge += v; break;
                }
            mfp.snmp_data = JsonConvert.SerializeObject(counter);

            if (printBlack        > 0) mfp.items.Add(new CounterItem { job_type = "print", color = "black",      size = "normal", sheets = printBlack });
            if (printColor        > 0) mfp.items.Add(new CounterItem { job_type = "print", color = "color_full", size = "normal", sheets = printColor });
            if (printDuotone      > 0) mfp.items.Add(new CounterItem { job_type = "print", color = "duotone",    size = "normal", sheets = printDuotone });
            if (printBlackLarge   > 0) mfp.items.Add(new CounterItem { job_type = "print", color = "black",      size = "large",  sheets = printBlackLarge });
            if (printColorLarge   > 0) mfp.items.Add(new CounterItem { job_type = "print", color = "color_full", size = "large",  sheets = printColorLarge });
            if (printDuotoneLarge > 0) mfp.items.Add(new CounterItem { job_type = "print", color = "duotone",    size = "large",  sheets = printDuotoneLarge });

            if (scanBlack         > 0) mfp.items.Add(new CounterItem { job_type = "scan",  color = "black",      size = "normal", sheets = scanBlack });
            if (scanColor         > 0) mfp.items.Add(new CounterItem { job_type = "scan",  color = "color_full", size = "normal", sheets = scanColor });
            if (scanDuotone       > 0) mfp.items.Add(new CounterItem { job_type = "scan",  color = "duotone",    size = "normal", sheets = scanDuotone });
            if (scanBlackLarge    > 0) mfp.items.Add(new CounterItem { job_type = "scan",  color = "black",      size = "large",  sheets = scanBlackLarge });
            if (scanColorLarge    > 0) mfp.items.Add(new CounterItem { job_type = "scan",  color = "color_full", size = "large",  sheets = scanColorLarge });
            if (scanDuotoneLarge  > 0) mfp.items.Add(new CounterItem { job_type = "scan",  color = "duotone",    size = "large",  sheets = scanDuotoneLarge });

            return Supplies_Toshiba(mfp);
        }

        private bool Counter_Kyocera(Printer mfp)
        {
            var ip = mfp.ip;
            Log(new LogInfo { Category = "SNMP", Function = "Counter_Kyocera", Option = "request", Identity = ip });
            var counter = KYOCERA_MIBs(ip);
            Log(new LogInfo { Category = "SNMP", Function = "Counter_Kyocera", Option = "response", Identity = ip, Message = JsonConvert.SerializeObject(counter) });
            if (counter == null) return false;

            int printBlack = 0, printColor = 0;
            int copyBlack = 0, copyColor = 0;
            int faxBlack = 0;

            foreach (var obj in counter)
                switch (obj.Key.ToString())
                {
                    case "1.3.6.1.4.1.1347.42.3.1.1.1.1.1": int.TryParse(obj.Value.ToString(), out printBlack); break;
                    case "1.3.6.1.4.1.1347.42.3.1.1.1.1.2": int.TryParse(obj.Value.ToString(), out printColor); break;
                    case "1.3.6.1.4.1.1347.42.3.1.2.1.1.1": int.TryParse(obj.Value.ToString(), out copyBlack);  break;
                    case "1.3.6.1.4.1.1347.42.3.1.2.1.1.2": int.TryParse(obj.Value.ToString(), out copyColor);  break;
                    case "1.3.6.1.4.1.1347.42.3.1.3.1.1.1": int.TryParse(obj.Value.ToString(), out faxBlack);   break;
                }
            mfp.snmp_data = JsonConvert.SerializeObject(counter);

            if (printBlack > 0) mfp.items.Add(new CounterItem { job_type = "print", color = "black",      size = "normal", sheets = printBlack });
            if (printColor > 0) mfp.items.Add(new CounterItem { job_type = "print", color = "color_full", size = "normal", sheets = printColor });
            if (copyBlack  > 0) mfp.items.Add(new CounterItem { job_type = "copy",  color = "black",      size = "normal", sheets = copyBlack });
            if (copyColor  > 0) mfp.items.Add(new CounterItem { job_type = "copy",  color = "color_full", size = "normal", sheets = copyColor });
            if (faxBlack   > 0) mfp.items.Add(new CounterItem { job_type = "fax",   color = "black",      size = "normal", sheets = faxBlack });

            return Supplies_Kyocera(mfp);
        }

        // ── Supplies ──────────────────────────────────────────────────────────

        private static void AddSupply(Printer mfp, string type, string color, int level, int capacity)
        {
            if (capacity > 0)
                mfp.supply_items.Add(new SupplyItem
                {
                    type = type,
                    color = color,
                    level = Math.Round((double)level / capacity, 2)
                });
        }

        // OID 索引對應（預設）：.1=toner_black / .2=toner_waste / .3=cyan / .4=magenta / .5=yellow
        private bool Supplies(Printer mfp)
        {
            var ip = mfp.ip;
            var capacity = Printer_MaxCapacity(ip);
            if (capacity == null) return false;
            int c1 = 0, c2 = 0, c3 = 0, c4 = 0, c5 = 0;
            foreach (var obj in capacity)
                switch (obj.Key.ToString())
                {
                    case "1.3.6.1.2.1.43.11.1.1.8.1.1": int.TryParse(obj.Value.ToString(), out c1); break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.2": int.TryParse(obj.Value.ToString(), out c2); break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.3": int.TryParse(obj.Value.ToString(), out c3); break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.4": int.TryParse(obj.Value.ToString(), out c4); break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.5": int.TryParse(obj.Value.ToString(), out c5); break;
                }

            var level = Printer_SuppliesLevel(ip);
            if (level == null) return false;
            int l1 = 0, l2 = 0, l3 = 0, l4 = 0, l5 = 0;
            foreach (var obj in level)
                switch (obj.Key.ToString())
                {
                    case "1.3.6.1.2.1.43.11.1.1.9.1.1": int.TryParse(obj.Value.ToString(), out l1); break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.2": int.TryParse(obj.Value.ToString(), out l2); break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.3": int.TryParse(obj.Value.ToString(), out l3); break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.4": int.TryParse(obj.Value.ToString(), out l4); break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.5": int.TryParse(obj.Value.ToString(), out l5); break;
                }

            AddSupply(mfp, "toner", "black",   l1, c1);
            AddSupply(mfp, "toner", "waste",   l2, c2);
            AddSupply(mfp, "toner", "cyan",    l3, c3);
            AddSupply(mfp, "toner", "magenta", l4, c4);
            AddSupply(mfp, "toner", "yellow",  l5, c5);
            return true;
        }

        // OID 索引對應（Kyocera 順序）：.4=toner_black / .5=waste / .1=cyan / .2=magenta / .3=yellow
        private bool Supplies_Kyocera(Printer mfp)
        {
            var ip = mfp.ip;
            var capacity = Printer_MaxCapacity(ip);
            if (capacity == null) return false;
            int capBlack = 0, capWaste = 0, capCyan = 0, capMagenta = 0, capYellow = 0;
            foreach (var obj in capacity)
                switch (obj.Key.ToString())
                {
                    case "1.3.6.1.2.1.43.11.1.1.8.1.4": int.TryParse(obj.Value.ToString(), out capBlack);   break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.5": int.TryParse(obj.Value.ToString(), out capWaste);   break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.1": int.TryParse(obj.Value.ToString(), out capCyan);    break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.2": int.TryParse(obj.Value.ToString(), out capMagenta); break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.3": int.TryParse(obj.Value.ToString(), out capYellow);  break;
                }

            var level = Printer_SuppliesLevel(ip);
            if (level == null) return false;
            int levBlack = 0, levWaste = 0, levCyan = 0, levMagenta = 0, levYellow = 0;
            foreach (var obj in level)
                switch (obj.Key.ToString())
                {
                    case "1.3.6.1.2.1.43.11.1.1.9.1.4": int.TryParse(obj.Value.ToString(), out levBlack);   break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.5": int.TryParse(obj.Value.ToString(), out levWaste);   break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.1": int.TryParse(obj.Value.ToString(), out levCyan);    break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.2": int.TryParse(obj.Value.ToString(), out levMagenta); break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.3": int.TryParse(obj.Value.ToString(), out levYellow);  break;
                }

            AddSupply(mfp, "toner", "black",   levBlack,   capBlack);
            AddSupply(mfp, "toner", "waste",   levWaste,   capWaste);
            AddSupply(mfp, "toner", "cyan",    levCyan,    capCyan);
            AddSupply(mfp, "toner", "magenta", levMagenta, capMagenta);
            AddSupply(mfp, "toner", "yellow",  levYellow,  capYellow);
            return true;
        }

        // OID 索引對應（Toshiba 順序）：.1=toner_black / .2=cyan / .3=magenta / .4=yellow / .5=waste
        private bool Supplies_Toshiba(Printer mfp)
        {
            var ip = mfp.ip;
            var capacity = Printer_MaxCapacity(ip);
            if (capacity == null) return false;
            int capBlack = 0, capCyan = 0, capMagenta = 0, capYellow = 0, capWaste = 0;
            foreach (var obj in capacity)
                switch (obj.Key.ToString())
                {
                    case "1.3.6.1.2.1.43.11.1.1.8.1.1": int.TryParse(obj.Value.ToString(), out capBlack);   break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.2": int.TryParse(obj.Value.ToString(), out capCyan);    break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.3": int.TryParse(obj.Value.ToString(), out capMagenta); break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.4": int.TryParse(obj.Value.ToString(), out capYellow);  break;
                    case "1.3.6.1.2.1.43.11.1.1.8.1.5": int.TryParse(obj.Value.ToString(), out capWaste);   break;
                }

            var level = Printer_SuppliesLevel(ip);
            if (level == null) return false;
            int levBlack = 0, levCyan = 0, levMagenta = 0, levYellow = 0, levWaste = 0;
            foreach (var obj in level)
                switch (obj.Key.ToString())
                {
                    case "1.3.6.1.2.1.43.11.1.1.9.1.1": int.TryParse(obj.Value.ToString(), out levBlack);   break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.2": int.TryParse(obj.Value.ToString(), out levCyan);    break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.3": int.TryParse(obj.Value.ToString(), out levMagenta); break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.4": int.TryParse(obj.Value.ToString(), out levYellow);  break;
                    case "1.3.6.1.2.1.43.11.1.1.9.1.5": int.TryParse(obj.Value.ToString(), out levWaste);   break;
                }

            AddSupply(mfp, "toner", "black",   levBlack,   capBlack);
            AddSupply(mfp, "toner", "cyan",    levCyan,    capCyan);
            AddSupply(mfp, "toner", "magenta", levMagenta, capMagenta);
            AddSupply(mfp, "toner", "yellow",  levYellow,  capYellow);
            AddSupply(mfp, "toner", "waste",   levWaste,   capWaste);
            return true;
        }

        // ── SNMP primitives ───────────────────────────────────────────────────

        private static Dictionary<Oid, AsnType> Printer_MAC(string ip)          => SNMPv1_GetBulk(ip, "1.3.6.1.2.1.2.2.1.6");
        private static Dictionary<Oid, AsnType> Printer_PrinterName(string ip)  => SNMPv1_GetBulk(ip, "1.3.6.1.2.1.43.5.1.1.16");
        private static Dictionary<Oid, AsnType> Printer_SerialNumber(string ip) => SNMPv1_GetBulk(ip, "1.3.6.1.2.1.43.5.1.1.17");
        private static Dictionary<Oid, AsnType> Printer_MaxCapacity(string ip)  => SNMPv1_GetBulk(ip, "1.3.6.1.2.1.43.11.1.1.8");
        private static Dictionary<Oid, AsnType> Printer_SuppliesLevel(string ip)=> SNMPv1_GetBulk(ip, "1.3.6.1.2.1.43.11.1.1.9");
        private static Dictionary<Oid, AsnType> Printer_Alert(string ip)        => SNMPv1_GetBulk(ip, "1.3.6.1.2.1.43.18.1.1");

        private static Dictionary<Oid, AsnType> COMMON_MIBs(string ip)  => SNMPv1_GetBulk(ip, "1.3.6.1.2.1.43.10.2.1.4.1.1");
        private static Dictionary<Oid, AsnType> XEROX_MIBs(string ip)   => SNMPv1_GetBulk(ip, "1.3.6.1.4.1.253.8.53.13.2.1.6.1.20");
        private static Dictionary<Oid, AsnType> RICOH_MIBs(string ip)   => SNMPv1_GetBulk(ip, "1.3.6.1.4.1.367.3.2.1.2.19.5.1.9");
        private static Dictionary<Oid, AsnType> TOSHIBA_MIBs(string ip) => SNMPv1_GetBulk(ip, "1.3.6.1.4.1.1129.2.3.50.1.3.21.6.1");
        private static Dictionary<Oid, AsnType> KYOCERA_MIBs(string ip) => SNMPv1_GetBulk(ip, "1.3.6.1.4.1.1347.42.3.1");

        /// <summary>
        /// 逐筆拉 GetNext（v1 community=public），收集所有以 rootOid 為 prefix 的 var bind。
        /// </summary>
        private static Dictionary<Oid, AsnType> SNMPv1_GetBulk(string ip, string oid)
        {
            var result = new Dictionary<Oid, AsnType>();
            var param = new AgentParameters { Version = SnmpVersion.Ver1 };
            param.Community.Set("public");

            var pdu = new Pdu { Type = PduType.GetNext };
            if (!IPAddress.TryParse(ip, out var addr)) return result;

            using (var target = new UdpTarget(addr, 161, 2000, 1))
            {
                var rootOid = new Oid(oid);
                var lastOid = (Oid)rootOid.Clone();

                while (lastOid != null)
                {
                    try
                    {
                        pdu.VbList.Clear();
                        pdu.VbList.Add(lastOid);
                        var pkt = (SnmpV1Packet)target.Request(pdu, param);
                        if (pkt == null) break;
                        if (pkt.Pdu.ErrorStatus != 0) break;

                        foreach (var v in pkt.Pdu.VbList)
                        {
                            if (rootOid.IsRootOf(v.Oid))
                            {
                                result.Add(v.Oid, v.Value);
                                lastOid = v.Oid;
                            }
                            else { lastOid = null; break; }
                        }
                    }
                    catch { lastOid = null; }
                }
            }
            return result;
        }
    }
}

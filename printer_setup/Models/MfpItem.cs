using DataClass;
using printer_setup.Infrastructure;

namespace printer_setup.Models
{
    /// <summary>
    /// ListView 顯示用的事務機包裝類別。
    /// 內含 DataClass.Printer（承載 SNMP 結果）+ UI 專用欄位（勾選/連線/回傳/起尾表）。
    /// color_sheets / black_sheets / large_sheets：由 SNMP 聚合後寫入，使用者可手動覆寫作為 起表/尾表。
    /// </summary>
    internal class MfpItem : ObservableBase
    {
        public Printer Inner { get; }

        public MfpItem() : this(new Printer()) { }
        public MfpItem(Printer p) { Inner = p ?? new Printer(); }

        public int id => Inner.id;
        public string code => Inner.code;
        public string name => Inner.name;
        public string model => Inner.model;
        public bool printer_counter => Inner.printer_counter;

        public string ip
        {
            get => Inner.ip;
            set { if (Inner.ip != value) { Inner.ip = value; Raise(); } }
        }
        public string mac
        {
            get => Inner.mac;
            set { if (Inner.mac != value) { Inner.mac = value; Raise(); } }
        }
        public string serial_number
        {
            get => Inner.serial_number;
            set { if (Inner.serial_number != value) { Inner.serial_number = value; Raise(); } }
        }
        public string printer_name
        {
            get => Inner.printer_name;
            set { if (Inner.printer_name != value) { Inner.printer_name = value; Raise(); } }
        }

        private bool _check;
        public bool check { get => _check; set => Set(ref _check, value); }

        private int _connect;   // 0=等待 1=失敗 2=成功
        public int connect { get => _connect; set => Set(ref _connect, value); }

        private int _upload;    // 0=等待 1=失敗 2=成功
        public int upload { get => _upload; set => Set(ref _upload, value); }

        private int _color_sheets;
        public int color_sheets { get => _color_sheets; set => Set(ref _color_sheets, value); }
        private int _black_sheets;
        public int black_sheets { get => _black_sheets; set => Set(ref _black_sheets, value); }
        private int _large_sheets;
        public int large_sheets { get => _large_sheets; set => Set(ref _large_sheets, value); }

        /// <summary>
        /// 以 Inner.items 聚合出 black/color/large_sheets 初值（SNMP 完成後呼叫）。
        /// black = 黑白 normal；color = 彩色/雙色 normal；large = size=large 合計。
        /// </summary>
        public void PopulateSheetsFromItems()
        {
            int b = 0, c = 0, l = 0;
            foreach (var it in Inner.items)
            {
                if (it.size == "large") l += it.sheets;
                else if (it.color == "black") b += it.sheets;
                else c += it.sheets;
            }
            black_sheets = b;
            color_sheets = c;
            large_sheets = l;
        }
    }

    /// <summary>
    /// 鍵值配對（ComboBox 用）。
    /// </summary>
    internal class Pair
    {
        public string Name { get; set; }
        public object Value { get; set; }
    }
}

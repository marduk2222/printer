using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DataClass;
using Lib;
using printer_setup.Commands;
using printer_setup.Infrastructure;
using printer_setup.Models;
using printer_setup.Services;

namespace printer_setup.ViewModels
{
    /// <summary>
    /// MainWindow 的主要 ViewModel。
    /// Stage 流程：
    ///   0 = 驗證帳號 / 1 = 選定客戶 / 2 = 選定事務機 / 3 = 新增 / 4 = 移除
    /// Verify：0 = 尚未 / 1 = 成功 / 2 = 失敗 / 3 = 連線異常
    ///
    /// 長操作（驗證、查詢、SNMP、回報）均為 async，透過 IsBusy 禁用其他按鈕。
    /// 新增/移除回報完成後自動回到 Stage 2 讓使用者繼續其他動作。
    /// </summary>
    internal class MainViewModel : ObservableBase
    {
        private readonly AsyncLogger _logger;
        private readonly ConfigSync _configSync;
        private readonly PrinterScanner _scanner;
        private readonly Dispatcher _uiDispatcher;

        private RestClient _rest;
        private int _partnerId;

        public MainViewModel(string baseDirectory, AsyncLogger logger)
        {
            _logger = logger;
            _uiDispatcher = Dispatcher.CurrentDispatcher;

            _partnerId = Setup.General.Partner;
            _rest = CreateClient(Setup.General.RestLogin, Setup.General.RestPassword);
            _configSync = new ConfigSync(baseDirectory, logger);
            _scanner = new PrinterScanner(logger, _rest);

            _logger?.Write(new LogInfo { Category = "Initialize", Function = "ctor", Option = "REST", Message = $"RestUrl={Setup.General.RestUrl}, Partner={_partnerId}" });

            // 快速（UI 當下完成）
            NextCommand        = new RelayCommand(_ => Stage++,                                        _ => !IsBusy);
            PrevCommand        = new RelayCommand(_ => Stage--,                                        _ => !IsBusy);
            ReturnCommand      = new RelayCommand(_ => GoToSelectDevice(),                             _ => !IsBusy);
            StageNewCommand    = new RelayCommand(_ => EnterActionStage(StageKind.Install),            _ => !IsBusy);
            StageRemoveCommand = new RelayCommand(_ => EnterActionStage(StageKind.Remove),             _ => !IsBusy);

            // 慢速（I/O：網路 / SNMP）
            VerifyCommand  = new AsyncRelayCommand(RunVerify);
            SearchCommand  = new AsyncRelayCommand(RunSearch);
            PrinterCommand = new AsyncRelayCommand(RunPrinterScan);
            TestCommand    = new AsyncRelayCommand(RunAddAndReport);
            RemoveCommand  = new AsyncRelayCommand(RunRemoveAndReport);
        }

        // ─── Bindable state ───────────────────────────────────────────────

        private StageKind _stageKind = StageKind.Login;
        public int Stage
        {
            get => (int)_stageKind;
            set { if ((int)_stageKind != value) { _stageKind = (StageKind)value; Raise(); } }
        }

        private VerifyState _verifyState = VerifyState.None;
        public int Verify
        {
            get => (int)_verifyState;
            set { if ((int)_verifyState != value) { _verifyState = (VerifyState)value; Raise(); } }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (Set(ref _isBusy, value))
                    _uiDispatcher.BeginInvoke(new Action(CommandManager.InvalidateRequerySuggested));
            }
        }

        private string _filter;
        public string Filter { get => _filter; set => Set(ref _filter, value); }

        public ObservableCollection<MfpItem> List  { get; } = new ObservableCollection<MfpItem>();
        public ObservableCollection<MfpItem> List2 { get; } = new ObservableCollection<MfpItem>();

        private List<Pair> _companyItems;
        public List<Pair> CompanyItems { get => _companyItems; set => Set(ref _companyItems, value); }

        private object _selectedCompanyValue;
        public object SelectedCompanyValue
        {
            get => _selectedCompanyValue;
            set { if (Set(ref _selectedCompanyValue, value)) _ = OnCompanyChangedAsync(); }
        }

        // ─── Commands ─────────────────────────────────────────────────────

        public ICommand VerifyCommand      { get; }
        public ICommand SearchCommand      { get; }
        public ICommand NextCommand        { get; }
        public ICommand PrevCommand        { get; }
        public ICommand ReturnCommand      { get; }
        public ICommand StageNewCommand    { get; }
        public ICommand StageRemoveCommand { get; }
        public ICommand PrinterCommand     { get; }
        public ICommand TestCommand        { get; }
        public ICommand RemoveCommand      { get; }

        /// <summary>View 透過 PasswordBox 讀值後觸發。</summary>
        public event Action AuthenticateRequested;

        private sealed class Credentials { public string Account; public string Password; }
        private TaskCompletionSource<Credentials> _credsFromView;

        /// <summary>View 呼叫：把 PasswordBox 讀到的帳密回傳給 VM。</summary>
        public void ProvideCredentials(string account, string password)
            => _credsFromView?.TrySetResult(new Credentials { Account = account, Password = password });

        // ─── Async actions ───────────────────────────────────────────────

        private async Task RunVerify()
        {
            using (BusyScope())
            {
                _credsFromView = new TaskCompletionSource<Credentials>();
                AuthenticateRequested?.Invoke();
                var creds = await _credsFromView.Task;

                var state = await Task.Run(() =>
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(creds.Account) && !string.IsNullOrEmpty(creds.Password))
                            _rest = CreateClient(creds.Account, creds.Password);
                        else
                            _rest = CreateClient(Setup.General.RestLogin, Setup.General.RestPassword);

                        if (_rest.Authenticate()) return VerifyState.Success;
                        return (_rest?.Ping() ?? false) ? VerifyState.Failed : VerifyState.ConnectionError;
                    }
                    catch (Exception ex)
                    {
                        _logger?.Write(new LogInfo { File = "Error", Category = "VerifyStaff", Option = "exception", Message = ex.Message });
                        return VerifyState.ConnectionError;
                    }
                });

                Verify = (int)state;
                _logger?.Write(new LogInfo { Category = "Run", Function = "Verify", Option = "response", Message = $"Verify={Verify}" });
                if (state == VerifyState.Success) Stage = (int)StageKind.SelectClient;
            }
        }

        private async Task RunSearch()
        {
            using (BusyScope())
            {
                var keyword = Filter?.Trim() ?? "";
                var response = await Task.Run(() => _rest?.GetCompanys(keyword));
                if (response == null)
                {
                    _logger?.Write(new LogInfo { Category = "Search", Message = "連線失敗或無回應" });
                    MessageBox.Show("無法連線到伺服器", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (response.status != "success" || response.data == null)
                {
                    _logger?.Write(new LogInfo { Category = "Search", Message = response.message ?? "查無資料" });
                    MessageBox.Show(response.message ?? "查無資料", "提示");
                    return;
                }
                _logger?.Write(new LogInfo { Message = $"公司數量=>{response.data.Count}筆" });
                if (response.data.Count == 0)
                {
                    _logger?.Write(new LogInfo { Message = $"查詢條件(\"{keyword}\")=>查無資料" });
                    MessageBox.Show("查無資料", "提示");
                    CompanyItems = null;
                    return;
                }
                foreach (var item in response.data)
                    _logger?.Write(new LogInfo { Category = "Search", Function = "Company", Option = "item", Message = $"id={item.id} / code={item.code} / name={item.name}" });

                var list = new List<Pair>();
                foreach (var item in response.data) list.Add(new Pair { Name = item.name, Value = item.code });
                CompanyItems = list;
                SelectedCompanyValue = list.Count > 0 ? list[0].Value : null;
            }
        }

        private async Task OnCompanyChangedAsync()
        {
            if (SelectedCompanyValue == null) return;
            if (!int.TryParse(SelectedCompanyValue.ToString(), out var pid) || pid <= 0)
            {
                _logger?.Write(new LogInfo { Category = "ComboBox", Option = "parse_fail", Message = $"SelectedValue={SelectedCompanyValue} 無法轉為正整數 Partner ID" });
                return;
            }

            _partnerId = pid;
            _logger?.Write(new LogInfo { Category = "ComboBox", Function = "GetPrinter", Option = "request", Message = $"partner_id={_partnerId}" });
            var printers = await Task.Run(() => _rest?.GetPrinter(_partnerId));
            if (printers == null)
            {
                _logger?.Write(new LogInfo { Category = "ComboBox", Message = $"Partner={_partnerId} 查無事務機或連線失敗" });
                return;
            }
            _logger?.Write(new LogInfo { Category = "ComboBox", Function = "GetPrinter", Option = "response", Message = $"事務機數量=>{printers.Count}筆（partner_id={_partnerId}）" });
            foreach (var p in printers)
                _logger?.Write(new LogInfo { Category = "ComboBox", Function = "GetPrinter", Option = "printer", Message = $"code={p.code} / name={p.name} / serial={p.serial_number} / ip={p.ip} / model={p.model}" });

            List.Clear();
            foreach (var p in printers) List.Add(new MfpItem(p));
        }

        private async Task RunPrinterScan()
        {
            using (BusyScope())
            {
                var targets = new List<MfpItem>();
                foreach (var item in List) if (item.check) targets.Add(item);

                await Task.Run(() =>
                {
                    foreach (var item in targets)
                    {
                        _logger?.Write(new LogInfo { Category = "Test", Option = item.ip, Message = $"[Test] ip:{item.ip} / serial:{item.serial_number} / name:{item.name} / model:{item.model}" });
                        try
                        {
                            item.Inner.items.Clear();
                            item.Inner.supply_items.Clear();
                            item.Inner.alerts.Clear();

                            var ok = _scanner.Scan(item.Inner);
                            item.connect = ok ? 2 : 1;
                            item.PopulateSheetsFromItems();
                            if (item.connect == 1) item.upload = 1;

                            _logger?.Write(new LogInfo { Category = "Test", Option = item.ip, Message = $"Black:{item.black_sheets}/Color:{item.color_sheets}/Large:{item.large_sheets} / Supplies:{item.Inner.supply_items.Count}" });
                        }
                        catch (Exception ex)
                        {
                            _logger?.Write(new LogInfo { Category = "Test", Option = "Exception", Message = ex.Message });
                        }
                    }
                });

                List2.Clear();
                foreach (var item in List) if (item.check) List2.Add(item);
            }
        }

        private async Task RunAddAndReport()
        {
            using (BusyScope())
            {
                await Task.Run(() =>
                {
                    foreach (var item in List2)
                    {
                        try
                        {
                            _logger?.Write(new LogInfo { Category = "Run", Function = "Install", Option = "request", Identity = item.code });

                            _rest?.UpdateDevice(code: item.code, mac: item.mac, ip: item.ip,
                                                serialNumber: item.serial_number, printerName: item.printer_name, isActive: true);

                            if (item.Inner.supply_items?.Count > 0)
                                _rest?.UpdateSupplies(new SuppliesRequest { code = item.code, items = item.Inner.supply_items });

                            var ok = _rest?.WriteMeter(new RecordRequest
                            {
                                code = item.code,
                                date = DateTime.Now.ToString("yyyy-MM-dd"),
                                state = 1,
                                data = item.Inner.snmp_data,
                                black_print = item.black_sheets,
                                color_print = item.color_sheets,
                                large_print = item.large_sheets,
                            }) ?? false;
                            item.upload = ok ? 2 : 1;

                            _logger?.Write(new LogInfo { Category = "Run", Function = "Install", Option = ok ? "success" : "failed", Identity = item.code });
                        }
                        catch (Exception ex)
                        {
                            item.upload = 1;
                            _logger?.Write(new LogInfo { File = "Error", Category = "Install", Identity = item.code, Message = ex.Message });
                        }
                    }
                });

                Setup.General.Partner = _partnerId;
                _configSync.CopyConfigToSubModules();
                _configSync.RunCommand("Service.bat");
                MessageBox.Show("您所選擇的設備已 新增成功!!!", "新增設備");
                GoToSelectDevice();
            }
        }

        private async Task RunRemoveAndReport()
        {
            using (BusyScope())
            {
                await Task.Run(() =>
                {
                    foreach (var item in List2)
                    {
                        try
                        {
                            _logger?.Write(new LogInfo { Category = "Run", Function = "Remove", Option = "request", Identity = item.code });

                            _rest?.UpdateDevice(code: item.code, mac: item.mac, ip: item.ip,
                                                serialNumber: item.serial_number, printerName: item.printer_name, isActive: false);

                            var ok = _rest?.WriteMeter(new RecordRequest
                            {
                                code = item.code,
                                date = DateTime.Now.ToString("yyyy-MM-dd"),
                                state = 2,
                                data = item.Inner.snmp_data,
                                black_print = item.black_sheets,
                                color_print = item.color_sheets,
                                large_print = item.large_sheets,
                            }) ?? false;
                            item.upload = ok ? 2 : 1;

                            _logger?.Write(new LogInfo { Category = "Run", Function = "Remove", Option = ok ? "success" : "failed", Identity = item.code });
                        }
                        catch (Exception ex)
                        {
                            item.upload = 1;
                            _logger?.Write(new LogInfo { File = "Error", Category = "Remove", Identity = item.code, Message = ex.Message });
                        }
                    }
                });

                MessageBox.Show("您所選擇的設備已 移除成功!!!", "移除設備");
                GoToSelectDevice();
            }
        }

        // ─── Stage transitions / Helpers ─────────────────────────────────

        private void GoToSelectDevice() => Stage = (int)StageKind.SelectDevice;

        private void EnterActionStage(StageKind target)
        {
            Stage = (int)target;
            List2.Clear();
            foreach (var item in List) if (item.check) List2.Add(item);
        }

        private RestClient CreateClient(string login, string password)
            => new RestClient(Setup.General.RestUrl, info => _logger?.Write(info),
                              Setup.General.RestDb, login, password);

        private IDisposable BusyScope()
        {
            IsBusy = true;
            return new ActionDisposable(() => IsBusy = false);
        }

        private sealed class ActionDisposable : IDisposable
        {
            private readonly Action _onDispose;
            public ActionDisposable(Action onDispose) { _onDispose = onDispose; }
            public void Dispose() { _onDispose?.Invoke(); }
        }
    }
}

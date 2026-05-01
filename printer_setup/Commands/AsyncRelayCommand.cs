using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace printer_setup.Commands
{
    /// <summary>
    /// 可等待的 ICommand 實作：
    ///  - 開始執行時 _running = true；結束後 _running = false
    ///  - CanExecute 由 _running 與外部 canExecute 共同決定
    ///  - 透過 CommandManager.RequerySuggested 觸發 UI 重查 CanExecute
    /// 用於 SNMP / REST 等長時間操作，避免 UI 重複點擊與卡頓。
    /// </summary>
    internal class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool> _canExecute;
        private bool _running;

        public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => !_running && (_canExecute?.Invoke() ?? true);

        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter)) return;
            _running = true;
            CommandManager.InvalidateRequerySuggested();
            try { await _execute(); }
            finally
            {
                _running = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}

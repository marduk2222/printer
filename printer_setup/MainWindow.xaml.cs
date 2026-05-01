using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using printer_setup.Infrastructure;
using printer_setup.ViewModels;

namespace printer_setup
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯。
    /// 只保留必要的 View 責任：建立 VM、載入 Logo、PasswordBox 讀值、關閉時 dispose logger。
    /// 所有業務邏輯集中在 ViewModels.MainViewModel 與 Services/*。
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly AsyncLogger _logger;
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();

            _logger = new AsyncLogger();
            _vm = new MainViewModel(AppDomain.CurrentDomain.BaseDirectory, _logger);

            // PasswordBox 沒有 DependencyProperty，只能由 View 讀值後回送給 VM。
            _vm.AuthenticateRequested += () => _vm.ProvideCredentials(staff_ac.Text?.Trim(), staff_pw.Password);

            DataContext = _vm;

            LoadLogo();
            Closing += (_, __) => _logger.Dispose();
        }

        private void LoadLogo()
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");
                if (File.Exists(path)) Logo.Source = new BitmapImage(new Uri(path, UriKind.Absolute));
            }
            catch { /* logo optional */ }
        }
    }
}

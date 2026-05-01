namespace printer_setup.ViewModels
{
    /// <summary>
    /// UI 精靈的步驟狀態。XAML DataTrigger 仍使用對應的整數值比對，
    /// 這個 enum 只在 C# 端作為常數使用，避免 ViewModel 內散佈 magic number。
    /// </summary>
    internal enum StageKind
    {
        Login        = 0,
        SelectClient = 1,
        SelectDevice = 2,
        Install      = 3,
        Remove       = 4,
    }

    /// <summary>
    /// 驗證結果。0=尚未 / 1=成功 / 2=帳密錯誤 / 3=連線異常。
    /// </summary>
    internal enum VerifyState
    {
        None             = 0,
        Success          = 1,
        Failed           = 2,
        ConnectionError  = 3,
    }
}

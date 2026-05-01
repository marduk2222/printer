using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace printer_setup
{
    /// <summary>
    /// MultiValueConverter：
    ///   values[0] = 目前 Stage (int)
    ///   values[1] = 逗號分隔的 Stage 清單 (string，例如 "1,2" 或 "3,4,5")
    /// 回傳 Visibility.Visible（屬於該清單）或 Collapsed。
    /// 用於 Footer 按鈕依 Stage 顯示/隱藏，取代大量 DataTrigger 樣式。
    /// </summary>
    internal class StageVisibilityConverter : IMultiValueConverter
    {
        public static readonly StageVisibilityConverter Instance = new StageVisibilityConverter();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2 || values[0] == null || values[1] == null)
                return Visibility.Collapsed;

            if (!int.TryParse(values[0].ToString(), out var stage)) return Visibility.Collapsed;
            var allowed = values[1].ToString();
            foreach (var token in allowed.Split(','))
            {
                if (int.TryParse(token.Trim(), out var v) && v == stage)
                    return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

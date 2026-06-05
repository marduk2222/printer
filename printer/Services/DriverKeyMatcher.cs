namespace printer.Services;

/// <summary>
/// driver_key 4 維比對：每維可用字面值或 *（wildcard）。
/// 用於 SheetTypeKey.DriverKey ↔ CounterItem.ToDriverKey() 的比對。
/// </summary>
public static class DriverKeyMatcher
{
    /// <summary>
    /// pattern 內可含 *；actualKey 必須是完整 4 維字面值。逐位元比對，* 視為任意。
    /// 兩者格式不正確（split 後不是 4 段）回傳 false。
    /// </summary>
    public static bool Matches(string pattern, string actualKey)
    {
        if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(actualKey)) return false;
        if (string.Equals(pattern, actualKey, System.StringComparison.OrdinalIgnoreCase)) return true;

        var p = pattern.Split('.');
        var a = actualKey.Split('.');
        if (p.Length != 4 || a.Length != 4) return false;

        for (int i = 0; i < 4; i++)
        {
            if (p[i] == "*") continue;
            if (!string.Equals(p[i], a[i], System.StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }
}

namespace printer.Data.Enums;

/// <summary>
/// 計費類型 (由設定欄位推導，用於顯示)
/// </summary>
public enum BillingType
{
    /// <summary>
    /// 純月租 - 每月固定費用
    /// </summary>
    Monthly = 1,

    /// <summary>
    /// 純張數 - 依列印張數計算
    /// </summary>
    PerPage = 2,

    /// <summary>
    /// 百分比 - 張數 × 百分比 × 單價
    /// </summary>
    Discount = 3,

    /// <summary>
    /// 階梯式 - 依張數匹配階梯單價
    /// </summary>
    Tiered = 4,

    /// <summary>
    /// 混合 - 月租費 + 張數計費
    /// </summary>
    Hybrid = 6
}

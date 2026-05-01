namespace printer.Data.Enums;

/// <summary>
/// 張數計費方式
/// </summary>
public enum PageMethod
{
    /// <summary>
    /// 不計算張數費
    /// </summary>
    None = 0,

    /// <summary>
    /// 純張數計費
    /// </summary>
    PerPage = 1,

    /// <summary>
    /// 百分比 (張數 × 百分比 × 單價)
    /// </summary>
    Discount = 2,

    /// <summary>
    /// 階梯式
    /// </summary>
    Tiered = 3
}

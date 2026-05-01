namespace printer.Data.Enums;

public static class EnumExtensions
{
    public static string ToDisplayName(this PageMethod method) => method switch
    {
        PageMethod.None => "無",
        PageMethod.PerPage => "純張數",
        PageMethod.Discount => "百分比",
        PageMethod.Tiered => "階梯式",
        _ => "未知"
    };

    public static string ToDisplayName(this BillingType type) => type switch
    {
        BillingType.Monthly => "純月租",
        BillingType.PerPage => "純張數",
        BillingType.Discount => "百分比",
        BillingType.Tiered => "階梯式",
        BillingType.Hybrid => "混合",
        _ => "未知"
    };
}

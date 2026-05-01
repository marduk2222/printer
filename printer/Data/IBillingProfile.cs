using printer.Data.Entities;
using printer.Data.Enums;

namespace printer.Data;

/// <summary>
/// 計費設定共用欄位介面 (PrinterBillingConfig 與 BillingTemplate 共用)
/// </summary>
public interface IBillingProfile
{
    decimal MonthlyFee { get; }
    PageMethod PageMethod { get; }
    decimal PricePerBlack { get; }
    decimal PricePerColor { get; }
    decimal PricePerLarge { get; }
    decimal DiscountPercentBlack { get; }
    decimal DiscountPercentColor { get; }
    decimal DiscountPercentLarge { get; }
    int FreeBlackPages { get; }
    int FreeColorPages { get; }
    int FreeLargePages { get; }
    int MonthlyFeeCycle { get; }
    int PageFeeCycle { get; }
    DateOnly? MonthlyStartDate { get; }
    DateOnly? PageStartDate { get; }
    ICollection<BillingTier> Tiers { get; }
}

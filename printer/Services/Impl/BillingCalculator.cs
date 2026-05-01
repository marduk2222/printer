using printer.Data;
using printer.Data.Entities;
using printer.Data.Enums;

namespace printer.Services.Impl;

/// <summary>
/// 計費計算器
/// 流程: 1. 扣除贈送張數 → 2. 依 PageMethod 計算張數費 → 3. 合計 = 月租 + 張數費
/// </summary>
public static class BillingCalculator
{
    /// <summary>
    /// 計算區間月數
    /// </summary>
    public static int CalcMonths(DateOnly start, DateOnly end)
        => (end.Year - start.Year) * 12 + end.Month - start.Month + 1;

    public static BillingCalculation Calculate(
        IBillingProfile profile,
        int printerId,
        int blackPages,
        int colorPages,
        int largePages,
        int periodMonths = 1,
        string printerName = "")
    {
        var lines = new List<string>();

        // Step 1: 依 PageMethod 計算 (誤印率先扣)
        // Step 2: 扣除贈送張數
        var pageFee = CalculatePageFee(profile, blackPages, colorPages, largePages, lines);

        // Step 3: 月租費 (固定金額，不乘以月數)
        var monthlyFee = profile.MonthlyFee;
        if (monthlyFee > 0)
        {
            lines.Add($"月租費: {monthlyFee:N0}");
        }

        return new BillingCalculation
        {
            PrinterId = printerId,
            PrinterName = printerName,
            BlackPages = blackPages,
            ColorPages = colorPages,
            LargePages = largePages,
            MonthlyFee = monthlyFee,
            PageFee = pageFee,
            TotalFee = monthlyFee + pageFee,
            BillingType = DeriveBillingType(profile),
            Breakdown = string.Join("\n", lines)
        };
    }

    /// <summary>
    /// 從計費欄位推導 BillingType (供顯示用)
    /// </summary>
    public static BillingType DeriveBillingType(IBillingProfile profile)
    {
        bool hasMonthly = profile.MonthlyFee > 0;
        bool hasPage = profile.PageMethod != PageMethod.None;

        if (hasMonthly && hasPage) return BillingType.Hybrid;
        if (hasMonthly) return BillingType.Monthly;

        return profile.PageMethod switch
        {
            PageMethod.PerPage => BillingType.PerPage,
            PageMethod.Discount => BillingType.Discount,
            PageMethod.Tiered => BillingType.Tiered,
            _ => BillingType.Monthly
        };
    }

    #region Tier JSON 解析 (Controller 共用)

    public class TierInput
    {
        public int TierType { get; set; }
        public int FromPages { get; set; }
        public int? ToPages { get; set; }
        public decimal Price { get; set; }
    }

    public static List<BillingTier> ParseTiers(string? tiersJson)
    {
        if (string.IsNullOrEmpty(tiersJson)) return new List<BillingTier>();

        try
        {
            var inputs = System.Text.Json.JsonSerializer.Deserialize<List<TierInput>>(tiersJson);
            if (inputs == null) return new List<BillingTier>();

            return inputs.Select((t, i) => new BillingTier
            {
                TierType = t.TierType,
                TierOrder = i + 1,
                FromPages = t.FromPages,
                ToPages = t.ToPages,
                Price = t.Price
            }).ToList();
        }
        catch
        {
            return new List<BillingTier>();
        }
    }

    /// <summary>
    /// 將 IBillingProfile 的計費欄位複製到目標 config
    /// </summary>
    public static void CopyProfile(IBillingProfile source, PrinterBillingConfig target)
    {
        target.MonthlyFee = source.MonthlyFee;
        target.PageMethod = source.PageMethod;
        target.PricePerBlack = source.PricePerBlack;
        target.PricePerColor = source.PricePerColor;
        target.PricePerLarge = source.PricePerLarge;
        target.DiscountPercentBlack = source.DiscountPercentBlack;
        target.DiscountPercentColor = source.DiscountPercentColor;
        target.DiscountPercentLarge = source.DiscountPercentLarge;
        target.FreeBlackPages = source.FreeBlackPages;
        target.FreeColorPages = source.FreeColorPages;
        target.FreeLargePages = source.FreeLargePages;
        target.MonthlyFeeCycle = source.MonthlyFeeCycle;
        target.PageFeeCycle = source.PageFeeCycle;
        target.MonthlyStartDate = source.MonthlyStartDate;
        target.PageStartDate = source.PageStartDate;

        target.Tiers = source.Tiers.Select(t => new BillingTier
        {
            TierType = t.TierType,
            TierOrder = t.TierOrder,
            FromPages = t.FromPages,
            ToPages = t.ToPages,
            Price = t.Price
        }).ToList();
    }

    #endregion

    #region 私有方法

    private static (int black, int color, int large) DeductFreePages(
        IBillingProfile profile, int black, int color, int large, List<string> lines)
    {
        if (profile.FreeBlackPages <= 0 && profile.FreeColorPages <= 0 && profile.FreeLargePages <= 0)
            return (black, color, large);

        var cb = Math.Max(0, black - profile.FreeBlackPages);
        var cc = Math.Max(0, color - profile.FreeColorPages);
        var cl = Math.Max(0, large - profile.FreeLargePages);

        lines.Add($"贈送張數: 黑白 {profile.FreeBlackPages} / 彩色 {profile.FreeColorPages} / 大張 {profile.FreeLargePages}");
        lines.Add($"計費張數: 黑白 {cb} / 彩色 {cc} / 大張 {cl}");

        return (cb, cc, cl);
    }

    private static decimal CalculatePageFee(
        IBillingProfile profile, int black, int color, int large, List<string> lines)
    {
        return profile.PageMethod switch
        {
            PageMethod.PerPage => CalcPerPage(profile, black, color, large, lines),
            PageMethod.Discount => CalcDiscount(profile, black, color, large, lines),
            PageMethod.Tiered => CalcTiered(profile, black, color, large, lines),
            _ => 0
        };
    }

    private static decimal CalcPerPage(
        IBillingProfile p, int black, int color, int large, List<string> lines)
    {
        // 扣除贈送張數
        var cb = Math.Max(0, black - p.FreeBlackPages);
        var cc = Math.Max(0, color - p.FreeColorPages);
        var cl = Math.Max(0, large - p.FreeLargePages);

        var bFee = cb * p.PricePerBlack;
        var cFee = cc * p.PricePerColor;
        var lFee = cl * p.PricePerLarge;

        lines.Add("張數計費: 純張數");
        if (black > 0) lines.Add($"  黑白: {black} - 贈送{p.FreeBlackPages} = {cb} 張 × {p.PricePerBlack:N4} = {bFee:N2}");
        if (color > 0) lines.Add($"  彩色: {color} - 贈送{p.FreeColorPages} = {cc} 張 × {p.PricePerColor:N4} = {cFee:N2}");
        if (large > 0) lines.Add($"  大張: {large} - 贈送{p.FreeLargePages} = {cl} 張 × {p.PricePerLarge:N4} = {lFee:N2}");

        return bFee + cFee + lFee;
    }

    private static decimal CalcDiscount(
        IBillingProfile p, int black, int color, int large, List<string> lines)
    {
        // 計費張數 = 實際張數 × (1 - 誤印率%) - 贈送張數
        var rateB = 1m - p.DiscountPercentBlack / 100m;
        var rateC = 1m - p.DiscountPercentColor / 100m;
        var rateL = 1m - p.DiscountPercentLarge / 100m;

        var afterRateB = (int)Math.Ceiling(black * rateB);
        var afterRateC = (int)Math.Ceiling(color * rateC);
        var afterRateL = (int)Math.Ceiling(large * rateL);

        var db = Math.Max(0, afterRateB - p.FreeBlackPages);
        var dc = Math.Max(0, afterRateC - p.FreeColorPages);
        var dl = Math.Max(0, afterRateL - p.FreeLargePages);

        var bFee = db * p.PricePerBlack;
        var cFee = dc * p.PricePerColor;
        var lFee = dl * p.PricePerLarge;

        lines.Add("張數計費: 誤印率 + 贈送扣抵");
        if (black > 0) lines.Add($"  黑白: {black} × (1-{p.DiscountPercentBlack}%) = {afterRateB} - 贈送{p.FreeBlackPages} = {db} 張 × {p.PricePerBlack:N4} = {bFee:N2}");
        if (color > 0) lines.Add($"  彩色: {color} × (1-{p.DiscountPercentColor}%) = {afterRateC} - 贈送{p.FreeColorPages} = {dc} 張 × {p.PricePerColor:N4} = {cFee:N2}");
        if (large > 0) lines.Add($"  大張: {large} × (1-{p.DiscountPercentLarge}%) = {afterRateL} - 贈送{p.FreeLargePages} = {dl} 張 × {p.PricePerLarge:N4} = {lFee:N2}");

        return bFee + cFee + lFee;
    }

    private static decimal CalcTiered(
        IBillingProfile p, int black, int color, int large, List<string> lines)
    {
        var allTiers = p.Tiers?.OrderBy(t => t.TierType).ThenBy(t => t.TierOrder).ToList()
            ?? new List<BillingTier>();
        if (!allTiers.Any()) return 0;

        // 扣除贈送張數
        var cb = Math.Max(0, black - p.FreeBlackPages);
        var cc = Math.Max(0, color - p.FreeColorPages);
        var cl = Math.Max(0, large - p.FreeLargePages);

        var blackTiers = allTiers.Where(t => t.TierType == 0).ToList();
        var colorTiers = allTiers.Where(t => t.TierType == 1).ToList();
        var largeTiers = allTiers.Where(t => t.TierType == 2).ToList();

        var bFee = MatchTier(cb, blackTiers);
        var cFee = MatchTier(cc, colorTiers);
        var lFee = MatchTier(cl, largeTiers);

        lines.Add("張數計費: 階梯式");
        void LogTiers(string label, List<BillingTier> tiers, int origPages, int chargePages, int free, decimal fee)
        {
            if (!tiers.Any()) return;
            foreach (var tier in tiers)
            {
                var to = tier.ToPages.HasValue ? tier.ToPages.Value.ToString("N0") : "∞";
                lines.Add($"  {label}: {tier.FromPages:N0}~{to} 張 = {tier.Price:N4}");
            }
            if (origPages > 0) lines.Add($"  {label}: {origPages} - 贈送{free} = {chargePages} 張 = {fee:N2}");
        }
        LogTiers("黑白", blackTiers, black, cb, p.FreeBlackPages, bFee);
        LogTiers("彩色", colorTiers, color, cc, p.FreeColorPages, cFee);
        LogTiers("大張", largeTiers, large, cl, p.FreeLargePages, lFee);

        return bFee + cFee + lFee;
    }

    /// <summary>
    /// 非累進階梯：找到張數所在區間，用該區間單價 × 全部張數
    /// </summary>
    private static decimal MatchTier(int totalPages, List<BillingTier> tiers)
    {
        if (totalPages <= 0 || !tiers.Any()) return 0;

        var tier = tiers.FirstOrDefault(t =>
            totalPages >= t.FromPages && (!t.ToPages.HasValue || totalPages <= t.ToPages.Value));

        tier ??= tiers.Last();

        return totalPages * tier.Price;
    }

    #endregion
}

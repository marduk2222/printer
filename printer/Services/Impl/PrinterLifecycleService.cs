using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;
using printer.Models;

namespace printer.Services.Impl;

/// <summary>
/// 事務機生命週期服務 — 換機、退機、客戶使用紀錄維護
/// </summary>
public class PrinterLifecycleService : IPrinterLifecycleService
{
    private readonly PrinterDbContext _context;

    public PrinterLifecycleService(PrinterDbContext context)
    {
        _context = context;
    }

    public async Task<Printer> ReplaceAsync(int oldPrinterId, ReplacePrinterRequest request)
    {
        var oldPrinter = await _context.Printers.FirstOrDefaultAsync(p => p.Id == oldPrinterId)
            ?? throw new ArgumentException("找不到要換機的事務機");
        if (!oldPrinter.PartnerId.HasValue)
            throw new InvalidOperationException("此事務機未指派客戶，無法換機（請改用一般編輯指派）");

        var date = request.Date ?? DateOnly.FromDateTime(DateTime.Today);
        var partnerId = oldPrinter.PartnerId.Value;

        // 1) 解析新機：庫存挑選或現場新建（擇一）
        Printer newPrinter;
        if (request.NewPrinterId.HasValue)
        {
            newPrinter = await _context.Printers.FirstOrDefaultAsync(p => p.Id == request.NewPrinterId.Value)
                ?? throw new ArgumentException("找不到指定的新機");
            if (newPrinter.Id == oldPrinter.Id)
                throw new InvalidOperationException("新機不可與舊機相同");
            if (!newPrinter.IsActive)
                throw new InvalidOperationException($"新機「{newPrinter.Name}」已停用");

            // 新機可為庫存機或「同一客戶」的既有設備；其他客戶的機器須先退機
            if (newPrinter.PartnerId.HasValue && newPrinter.PartnerId.Value != partnerId)
                throw new InvalidOperationException($"新機「{newPrinter.Name}」目前指派給其他客戶，須先退機");
        }
        else if (request.NewPrinter != null && !string.IsNullOrWhiteSpace(request.NewPrinter.Name))
        {
            var data = request.NewPrinter;
            var code = data.Code;
            if (string.IsNullOrWhiteSpace(code))
                code = await GeneratePrinterCodeAsync();
            else if (await _context.Printers.AnyAsync(p => p.Code == code))
                throw new InvalidOperationException($"事務機代碼「{code}」已存在");

            newPrinter = new Printer
            {
                Code = code,
                Name = data.Name,
                BrandId = data.BrandId,
                ModelId = data.ModelId,
                SerialNumber = data.SerialNumber,
                Ip = data.Ip,
                Mac = data.Mac,
                PrinterName = data.PrinterName,
                Description = data.Description,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Printers.Add(newPrinter);
        }
        else
        {
            throw new ArgumentException("請指定接手的新機（NewPrinterId）或提供新機資料（NewPrinter.Name 必填）");
        }

        // 2) 補登舊機尾表（選填；機器已拆除無法抄表時由人工輸入）
        AddFinalMeterRecord(oldPrinter, partnerId, date, request.FinalMeter);

        // 3) 關閉舊機使用紀錄（reason=replace、記錄接手新機）
        await CloseOpenUsageAsync(oldPrinter, date, "replace", newPrinter, request.Note);

        // 4) 新機接手：客戶、合約、群組、換機鏈，並開新使用紀錄
        newPrinter.PartnerId = partnerId;
        newPrinter.PartnerNumber = oldPrinter.PartnerNumber;
        newPrinter.ContractStartDate = oldPrinter.ContractStartDate;
        newPrinter.ContractEndDate = oldPrinter.ContractEndDate;
        newPrinter.Deposit = oldPrinter.Deposit;
        newPrinter.BillingGroupId = oldPrinter.BillingGroupId;   // 群組成員資格轉移（張數接續累計）
        newPrinter.ReplacedFromPrinterId = oldPrinter.Id;
        newPrinter.UpdatedAt = DateTime.UtcNow;

        // 開新機使用紀錄；若新機本來就是同客戶的設備（已有使用中紀錄）則沿用不重開。
        // 注意：跨客戶調機時剛被 CloseOpenUsageAsync 關閉的紀錄仍會被查回（identity map），
        // 須以記憶體中的 EndDate 判斷是否真的仍為使用中。
        var newOpenUsage = newPrinter.Id > 0
            ? await _context.PrinterUsageRecords
                .FirstOrDefaultAsync(u => u.PrinterId == newPrinter.Id && u.EndDate == null)
            : null;
        if (newOpenUsage == null || newOpenUsage.EndDate != null)
        {
            _context.PrinterUsageRecords.Add(new PrinterUsageRecord
            {
                Printer = newPrinter,
                PartnerId = partnerId,
                StartDate = date,
                Note = request.Note
            });
        }

        // 5) 複製計費設定
        if (request.CopyBilling)
            await CopyBillingAsync(oldPrinter.Id, newPrinter);

        // 6) 舊機回庫存
        oldPrinter.PartnerId = null;
        oldPrinter.PartnerNumber = null;
        oldPrinter.ContractEndDate = date;
        oldPrinter.BillingGroupId = null;
        oldPrinter.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return newPrinter;
    }

    public async Task<Printer> ReturnAsync(int printerId, ReturnPrinterRequest request)
    {
        var printer = await _context.Printers.FirstOrDefaultAsync(p => p.Id == printerId)
            ?? throw new ArgumentException("找不到要退機的事務機");
        if (!printer.PartnerId.HasValue)
            throw new InvalidOperationException("此事務機未指派客戶，無須退機");

        var date = request.Date ?? DateOnly.FromDateTime(DateTime.Today);

        // 補登尾表（選填；已事先補登抄表者留空）
        AddFinalMeterRecord(printer, printer.PartnerId.Value, date, request.FinalMeter);

        await CloseOpenUsageAsync(printer, date, "return", null, request.Note);

        printer.PartnerId = null;
        printer.PartnerNumber = null;
        printer.ContractEndDate = date;
        printer.BillingGroupId = null;
        printer.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return printer;
    }

    public async Task<Printer> AssignAsync(int printerId, AssignPrinterRequest request)
    {
        var printer = await _context.Printers.FirstOrDefaultAsync(p => p.Id == printerId)
            ?? throw new ArgumentException("找不到要指派的事務機");
        if (printer.PartnerId.HasValue)
            throw new InvalidOperationException($"「{printer.Name}」已指派給客戶，請先退機或改用換機");
        if (!printer.IsActive)
            throw new InvalidOperationException($"「{printer.Name}」已停用");

        var partner = await _context.Partners.FirstOrDefaultAsync(p => p.Id == request.PartnerId)
            ?? throw new ArgumentException("找不到指定的客戶");

        var date = request.Date ?? DateOnly.FromDateTime(DateTime.Today);

        printer.PartnerId = partner.Id;
        printer.PartnerNumber = partner.PartnerNumber;
        printer.UpdatedAt = DateTime.UtcNow;

        _context.PrinterUsageRecords.Add(new PrinterUsageRecord
        {
            PrinterId = printer.Id,
            PartnerId = partner.Id,
            StartDate = date,
            Note = request.Note
        });

        await _context.SaveChangesAsync();
        return printer;
    }

    public void RecordPartnerChange(Printer printer, int? oldPartnerId, int? newPartnerId, DateOnly? date = null)
    {
        if (oldPartnerId == newPartnerId) return;
        var d = date ?? DateOnly.FromDateTime(DateTime.Today);

        if (oldPartnerId.HasValue)
        {
            // 同步查詢已載入或資料庫中的使用中紀錄
            var open = _context.PrinterUsageRecords.Local
                .FirstOrDefault(u => u.PrinterId == printer.Id && u.EndDate == null)
                ?? _context.PrinterUsageRecords
                .FirstOrDefault(u => u.PrinterId == printer.Id && u.EndDate == null);

            if (open != null)
            {
                open.EndDate = d;
                open.EndReason = "transfer";
                open.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // 舊資料沒有使用紀錄 → 以合約開始日（或建立日）補一筆完整紀錄
                _context.PrinterUsageRecords.Add(new PrinterUsageRecord
                {
                    PrinterId = printer.Id,
                    PartnerId = oldPartnerId.Value,
                    StartDate = FallbackStartDate(printer),
                    EndDate = d,
                    EndReason = "transfer"
                });
            }
        }

        if (newPartnerId.HasValue)
        {
            _context.PrinterUsageRecords.Add(new PrinterUsageRecord
            {
                Printer = printer,
                PartnerId = newPartnerId.Value,
                StartDate = d
            });
        }
    }

    public async Task<(List<PrinterUsageRecord> Records, int TotalDays)> GetUsageAsync(int printerId)
    {
        var records = await _context.PrinterUsageRecords
            .Include(u => u.Partner)
            .Include(u => u.ReplacementPrinter)
            .Where(u => u.PrinterId == printerId)
            .OrderByDescending(u => u.StartDate).ThenByDescending(u => u.Id)
            .ToListAsync();

        return (records, records.Sum(r => r.DurationDays));
    }

    #region Helpers

    /// <summary>
    /// 關閉機器的使用中紀錄；無紀錄（舊資料）時以合約開始日補一筆完整紀錄。不呼叫 SaveChanges。
    /// </summary>
    private async Task CloseOpenUsageAsync(Printer printer, DateOnly endDate, string reason, Printer? replacement, string? note)
    {
        var open = await _context.PrinterUsageRecords
            .FirstOrDefaultAsync(u => u.PrinterId == printer.Id && u.EndDate == null);

        if (open != null)
        {
            open.EndDate = endDate;
            open.EndReason = reason;
            open.ReplacementPrinter = replacement;
            if (!string.IsNullOrWhiteSpace(note)) open.Note = note;
            open.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.PrinterUsageRecords.Add(new PrinterUsageRecord
            {
                PrinterId = printer.Id,
                PartnerId = printer.PartnerId!.Value,
                StartDate = FallbackStartDate(printer),
                EndDate = endDate,
                EndReason = reason,
                ReplacementPrinter = replacement,
                Note = note
            });
        }
    }

    /// <summary>舊資料無使用紀錄時的起始日：合約開始日 → 機器建立日。</summary>
    private static DateOnly FallbackStartDate(Printer printer)
        => printer.ContractStartDate ?? DateOnly.FromDateTime(printer.CreatedAt.ToLocalTime());

    /// <summary>
    /// 補登尾表：以離機日建立一筆尾表抄表記錄（state=2）。
    /// finalMeter 為 SheetTypeId → 張數；空值或無任何項目時不建立。不呼叫 SaveChanges。
    /// </summary>
    private void AddFinalMeterRecord(Printer printer, int partnerId, DateOnly date, Dictionary<int, int>? finalMeter)
    {
        if (finalMeter == null) return;
        var entries = finalMeter.Where(kv => kv.Value > 0).ToList();
        if (entries.Count == 0) return;

        var record = new PrintRecord
        {
            PrinterId = printer.Id,
            PartnerId = partnerId,
            Date = date,
            State = "2",   // 尾表（與 printer_setup 退機/換機上傳一致）
            Count = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var sort = 1;
        foreach (var kv in entries)
        {
            record.Values.Add(new PrintRecordValue
            {
                SheetTypeId = kv.Key,
                Value = kv.Value,
                SortOrder = sort++
            });
        }
        _context.PrintRecords.Add(record);
    }

    /// <summary>
    /// 複製舊機計費設定（月租設定 + 各張數類型單價/階梯 + 舊式階梯）到新機。
    /// 新機（庫存機）原有的計費設定會先移除，避免重複。不呼叫 SaveChanges。
    /// </summary>
    private async Task CopyBillingAsync(int oldPrinterId, Printer newPrinter)
    {
        // 清除新機既有設定（庫存機可能殘留上一輪的計費設定）
        if (newPrinter.Id > 0)
        {
            var existingConfigs = await _context.PrinterBillingConfigs
                .Include(c => c.Tiers)
                .Where(c => c.PrinterId == newPrinter.Id).ToListAsync();
            _context.PrinterBillingConfigs.RemoveRange(existingConfigs);

            var existingPrices = await _context.PrinterBillingSheetPrices
                .Where(p => p.PrinterId == newPrinter.Id).ToListAsync();
            _context.PrinterBillingSheetPrices.RemoveRange(existingPrices);

            var existingTiers = await _context.PrinterBillingSheetTiers
                .Where(t => t.PrinterId == newPrinter.Id).ToListAsync();
            _context.PrinterBillingSheetTiers.RemoveRange(existingTiers);
        }

        var oldConfig = await _context.PrinterBillingConfigs
            .Include(c => c.Tiers)
            .FirstOrDefaultAsync(c => c.PrinterId == oldPrinterId);

        if (oldConfig != null)
        {
            var newConfig = new PrinterBillingConfig
            {
                Printer = newPrinter,
                IsEnabled = oldConfig.IsEnabled,
                MonthlyFee = oldConfig.MonthlyFee,
                PageMethod = oldConfig.PageMethod,
                PricePerBlack = oldConfig.PricePerBlack,
                PricePerColor = oldConfig.PricePerColor,
                PricePerLarge = oldConfig.PricePerLarge,
                DiscountPercentBlack = oldConfig.DiscountPercentBlack,
                DiscountPercentColor = oldConfig.DiscountPercentColor,
                DiscountPercentLarge = oldConfig.DiscountPercentLarge,
                FreeBlackPages = oldConfig.FreeBlackPages,
                FreeColorPages = oldConfig.FreeColorPages,
                FreeLargePages = oldConfig.FreeLargePages,
                MonthlyFeeCycle = oldConfig.MonthlyFeeCycle,
                PageFeeCycle = oldConfig.PageFeeCycle,
                MonthlyStartDate = oldConfig.MonthlyStartDate,
                PageStartDate = oldConfig.PageStartDate,
                LastMonthlyBilledDate = oldConfig.LastMonthlyBilledDate,
                LastPageBilledDate = oldConfig.LastPageBilledDate,
                Note = oldConfig.Note,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            foreach (var tier in oldConfig.Tiers)
            {
                newConfig.Tiers.Add(new BillingTier
                {
                    TierType = tier.TierType,
                    TierOrder = tier.TierOrder,
                    FromPages = tier.FromPages,
                    ToPages = tier.ToPages,
                    Price = tier.Price
                });
            }
            _context.PrinterBillingConfigs.Add(newConfig);
        }

        // 張數類型單價
        var oldSheetPrices = await _context.PrinterBillingSheetPrices
            .Where(p => p.PrinterId == oldPrinterId).ToListAsync();
        foreach (var sp in oldSheetPrices)
        {
            _context.PrinterBillingSheetPrices.Add(new PrinterBillingSheetPrice
            {
                Printer = newPrinter,
                SheetTypeId = sp.SheetTypeId,
                UnitPrice = sp.UnitPrice,
                DiscountPercent = sp.DiscountPercent,
                FreePages = sp.FreePages,
                Weight = sp.Weight,
                OffsetOrder = sp.OffsetOrder,
                SortOrder = sp.SortOrder,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // 張數類型階梯
        var oldSheetTiers = await _context.PrinterBillingSheetTiers
            .Where(t => t.PrinterId == oldPrinterId).ToListAsync();
        foreach (var st in oldSheetTiers)
        {
            _context.PrinterBillingSheetTiers.Add(new PrinterBillingSheetTier
            {
                Printer = newPrinter,
                SheetTypeId = st.SheetTypeId,
                TierOrder = st.TierOrder,
                FromPages = st.FromPages,
                ToPages = st.ToPages,
                Price = st.Price
            });
        }
    }

    /// <summary>產生事務機代碼：yyyyMM + 3 碼流水號（沿用事務機建立頁的規則）。</summary>
    private async Task<string> GeneratePrinterCodeAsync()
    {
        var prefix = DateTime.Now.ToString("yyyyMM");
        var maxCode = await _context.Printers
            .Where(p => p.Code.StartsWith(prefix))
            .OrderByDescending(p => p.Code)
            .Select(p => p.Code)
            .FirstOrDefaultAsync();

        var seq = 1;
        if (maxCode != null && maxCode.Length > prefix.Length && int.TryParse(maxCode.Substring(prefix.Length), out var n))
            seq = n + 1;
        return $"{prefix}{seq:D3}";
    }

    #endregion
}

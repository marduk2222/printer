using printer.Data.Entities;
using printer.Models;

namespace printer.Services;

/// <summary>
/// 事務機生命週期服務 — 換機、退機、客戶使用紀錄維護
/// </summary>
public interface IPrinterLifecycleService
{
    /// <summary>
    /// 換機：舊機回庫存，新機（庫存挑選或現場新建）接手客戶、合約與計費設定，並寫入使用紀錄與換機鏈。
    /// 回傳接手的新機。
    /// </summary>
    Task<Printer> ReplaceAsync(int oldPrinterId, ReplacePrinterRequest request);

    /// <summary>
    /// 退機：關閉使用紀錄、機器回庫存（清除客戶與群組、合約結束日填退機日）。
    /// </summary>
    Task<Printer> ReturnAsync(int printerId, ReturnPrinterRequest request);

    /// <summary>
    /// 指派：把庫存機指派給客戶並開使用紀錄（printer_setup 新增設備用）。
    /// </summary>
    Task<Printer> AssignAsync(int printerId, AssignPrinterRequest request);

    /// <summary>
    /// 客戶異動時維護使用紀錄（提供事務機 Create/Edit 呼叫）。
    /// 關閉舊客戶的使用中紀錄（reason=transfer）、為新客戶開新紀錄。不呼叫 SaveChanges。
    /// </summary>
    void RecordPartnerChange(Printer printer, int? oldPartnerId, int? newPartnerId, DateOnly? date = null);

    /// <summary>
    /// 取得機器的使用紀錄（新到舊）與已工作總時長（天）。
    /// </summary>
    Task<(List<PrinterUsageRecord> Records, int TotalDays)> GetUsageAsync(int printerId);
}

using printer.Data.Entities;
using printer.Data.Enums;

namespace printer.Services;

/// <summary>
/// 計費計算結果
/// </summary>
public class BillingCalculation
{
    public int PrinterId { get; set; }
    public string PrinterName { get; set; } = string.Empty;
    public BillingType BillingType { get; set; }
    public int BlackPages { get; set; }
    public int ColorPages { get; set; }
    public int LargePages { get; set; }
    public decimal MonthlyFee { get; set; }
    public decimal PageFee { get; set; }
    public decimal TotalFee { get; set; }
    public string Breakdown { get; set; } = string.Empty;
    public DateOnly? FirstRecordDate { get; set; }
    public DateOnly? LastRecordDate { get; set; }
    public int RecordCount { get; set; }
}

/// <summary>
/// 群組成員月租計算（月租仍綁在每台 PrinterBillingConfig）
/// </summary>
public class GroupMemberMonthlyFee
{
    public int PrinterId { get; set; }
    public string PrinterName { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public decimal MonthlyFee { get; set; }
}

/// <summary>
/// 計費群組合併計算結果（張數合併計費，月租按成員分項）
/// </summary>
public class GroupBillingCalculation
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int BillingPartnerId { get; set; }
    public List<GroupMemberMonthlyFee> Members { get; set; } = new();
    public decimal TotalMonthlyFee => Members.Sum(m => m.MonthlyFee);
    public decimal PageFee { get; set; }
    public string Breakdown { get; set; } = string.Empty;
    public DateOnly? FirstRecordDate { get; set; }
    public DateOnly? LastRecordDate { get; set; }
    public int RecordCount { get; set; }
}

/// <summary>
/// 計費服務介面
/// </summary>
public interface IBillingService
{
    #region 計費設定管理

    /// <summary>
    /// 取得事務機的計費設定
    /// </summary>
    Task<PrinterBillingConfig?> GetConfigAsync(int printerId);

    /// <summary>
    /// 取得所有計費設定
    /// </summary>
    Task<List<PrinterBillingConfig>> GetAllConfigsAsync();

    /// <summary>
    /// 儲存計費設定
    /// </summary>
    Task<PrinterBillingConfig> SaveConfigAsync(PrinterBillingConfig config);

    /// <summary>
    /// 刪除計費設定
    /// </summary>
    Task<bool> DeleteConfigAsync(int printerId);

    #endregion

    #region 計費模板

    /// <summary>
    /// 取得所有計費模板
    /// </summary>
    Task<List<BillingTemplate>> GetTemplatesAsync();

    /// <summary>
    /// 取得計費模板
    /// </summary>
    Task<BillingTemplate?> GetTemplateAsync(int id);

    /// <summary>
    /// 儲存計費模板
    /// </summary>
    Task<BillingTemplate> SaveTemplateAsync(BillingTemplate template);

    /// <summary>
    /// 刪除計費模板
    /// </summary>
    Task<bool> DeleteTemplateAsync(int id);

    /// <summary>
    /// 套用模板到事務機
    /// </summary>
    Task<PrinterBillingConfig> ApplyTemplateAsync(int printerId, int templateId);

    #endregion

    #region 費用計算

    /// <summary>
    /// 計算單台事務機的費用
    /// </summary>
    Task<BillingCalculation> CalculateAsync(int printerId, DateOnly startDate, DateOnly endDate);

    /// <summary>
    /// 計算客戶的總費用
    /// </summary>
    Task<List<BillingCalculation>> CalculatePartnerAsync(int partnerId, DateOnly startDate, DateOnly endDate);

    /// <summary>
    /// 計算單一計費群組的合併費用（張數合併、套群組層級設定）
    /// </summary>
    Task<GroupBillingCalculation> CalculateGroupAsync(int groupId, DateOnly startDate, DateOnly endDate);

    #endregion

    #region 計費群組

    Task<List<PrinterBillingGroup>> GetGroupsAsync();
    Task<PrinterBillingGroup?> GetGroupAsync(int id);
    Task<PrinterBillingGroup> SaveGroupAsync(PrinterBillingGroup group);
    Task<bool> DeleteGroupAsync(int id);
    Task<bool> SetPrinterGroupAsync(int printerId, int? groupId);

    /// <summary>
    /// 套用計費模板到群組（覆蓋張數類型單價、誤印率、贈送張數、階梯與計費週期）
    /// </summary>
    Task<PrinterBillingGroup> ApplyTemplateToGroupAsync(int groupId, int templateId);

    #endregion

    #region 帳單管理

    /// <summary>
    /// 取得帳單列表
    /// </summary>
    Task<List<Invoice>> GetInvoicesAsync(int? partnerId = null, string? status = null);

    /// <summary>
    /// 取得帳單詳情
    /// </summary>
    Task<Invoice?> GetInvoiceAsync(int id);

    /// <summary>
    /// 生成帳單
    /// </summary>
    Task<Invoice> GenerateInvoiceAsync(int partnerId, DateOnly startDate, DateOnly endDate);

    /// <summary>
    /// 批次生成帳單
    /// </summary>
    Task<List<Invoice>> BatchGenerateInvoicesAsync(DateOnly startDate, DateOnly endDate);

    /// <summary>
    /// 確認帳單
    /// </summary>
    Task<bool> ConfirmInvoiceAsync(int id);

    /// <summary>
    /// 標記已付款
    /// </summary>
    Task<bool> MarkAsPaidAsync(int id);

    /// <summary>
    /// 取消帳單
    /// </summary>
    Task<bool> CancelInvoiceAsync(int id);

    #endregion
}

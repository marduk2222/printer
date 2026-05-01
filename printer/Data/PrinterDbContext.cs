using Microsoft.EntityFrameworkCore;
using printer.Data.Entities;

namespace printer.Data;

/// <summary>
/// 印表機雲端抄表資料庫上下文
/// </summary>
public class PrinterDbContext : DbContext
{
    public PrinterDbContext(DbContextOptions<PrinterDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// 客戶
    /// </summary>
    public DbSet<Partner> Partners { get; set; }

    /// <summary>
    /// 品牌
    /// </summary>
    public DbSet<Brand> Brands { get; set; }

    /// <summary>
    /// 型號
    /// </summary>
    public DbSet<PrinterModel> PrinterModels { get; set; }

    /// <summary>
    /// 事務機
    /// </summary>
    public DbSet<Printer> Printers { get; set; }

    /// <summary>
    /// 張數回傳紀錄
    /// </summary>
    public DbSet<PrintRecord> PrintRecords { get; set; }

    /// <summary>
    /// 告警回傳紀錄
    /// </summary>
    public DbSet<AlertRecord> AlertRecords { get; set; }

    /// <summary>
    /// 客戶連絡人
    /// </summary>
    public DbSet<PartnerContact> PartnerContacts { get; set; }

    /// <summary>
    /// 客戶帳單寄送資訊
    /// </summary>
    public DbSet<PartnerBillingInfo> PartnerBillingInfos { get; set; }

    ///<summary>
    /// 系統使用者
    /// </summary>
    public DbSet<AppUser> AppUsers { get; set; }

    /// <summary>
    /// 事務機維護人員
    /// </summary>
    public DbSet<PrinterMaintainer> PrinterMaintainers { get; set; }

    /// <summary>
    /// 角色權限
    /// </summary>
    public DbSet<RolePermission> RolePermissions { get; set; }

    /// <summary>
    /// 電子發票
    /// </summary>
    public DbSet<Einvoice> Einvoices { get; set; }

    /// <summary>
    /// 電子發票明細
    /// </summary>
    public DbSet<EinvoiceItem> EinvoiceItems { get; set; }

    /// <summary>
    /// 發票平台設定
    /// </summary>
    public DbSet<EinvoicePlatform> EinvoicePlatforms { get; set; }

    /// <summary>
    /// 發票平台欄位對應
    /// </summary>
    public DbSet<EinvoiceFieldMapping> EinvoiceFieldMappings { get; set; }

    #region 模組化計費系統

    /// <summary>
    /// 系統模組
    /// </summary>
    public DbSet<SystemModule> SystemModules { get; set; }

    /// <summary>
    /// 客戶模組權限
    /// </summary>
    public DbSet<PartnerModule> PartnerModules { get; set; }

    /// <summary>
    /// 事務機計費設定
    /// </summary>
    public DbSet<PrinterBillingConfig> PrinterBillingConfigs { get; set; }

    /// <summary>
    /// 帳單
    /// </summary>
    public DbSet<Invoice> Invoices { get; set; }

    /// <summary>
    /// 帳單明細
    /// </summary>
    public DbSet<InvoiceItem> InvoiceItems { get; set; }

    /// <summary>
    /// 計費模板
    /// </summary>
    public DbSet<BillingTemplate> BillingTemplates { get; set; }

    public DbSet<BillingTemplateSheetPrice> BillingTemplateSheetPrices { get; set; }

    public DbSet<BillingTemplateSheetTier> BillingTemplateSheetTiers { get; set; }

    /// <summary>
    /// 計費階梯設定
    /// </summary>
    public DbSet<BillingTier> BillingTiers { get; set; }

    /// <summary>
    /// 計費群組（多台事務機合併計算超印費）
    /// </summary>
    public DbSet<PrinterBillingGroup> PrinterBillingGroups { get; set; }

    /// <summary>
    /// 計費群組各張數類型的單價/誤印率/贈送
    /// </summary>
    public DbSet<BillingGroupSheetPrice> BillingGroupSheetPrices { get; set; }

    /// <summary>
    /// 計費群組各張數類型的階梯
    /// </summary>
    public DbSet<BillingGroupSheetTier> BillingGroupSheetTiers { get; set; }

    /// <summary>
    /// 帳單列印設定
    /// </summary>
    public DbSet<InvoicePrintSettings> InvoicePrintSettings { get; set; }

    #endregion

    #region 彈性張數類型系統

    /// <summary>
    /// 全域張數類型定義
    /// </summary>
    public DbSet<SheetType> SheetTypes { get; set; }

    /// <summary>
    /// 張數類型對應的驅動參數
    /// </summary>
    public DbSet<SheetTypeKey> SheetTypeKeys { get; set; }

    /// <summary>
    /// 機型與張數類型的關聯（多對多）
    /// </summary>
    public DbSet<PrinterModelSheetType> PrinterModelSheetTypes { get; set; }

    /// <summary>
    /// 每台事務機的張數類型計費單價
    /// </summary>
    public DbSet<PrinterBillingSheetPrice> PrinterBillingSheetPrices { get; set; }

    /// <summary>
    /// 每台事務機各張數類型的階梯計費
    /// </summary>
    public DbSet<PrinterBillingSheetTier> PrinterBillingSheetTiers { get; set; }

    /// <summary>
    /// 抄表記錄中每個張數類型的數值
    /// </summary>
    public DbSet<PrintRecordValue> PrintRecordValues { get; set; }

    /// <summary>
    /// 張數類型互換比例設定
    /// </summary>
    public DbSet<SheetTypeConversionRate> SheetTypeConversionRates { get; set; }

    #endregion

    #region 派工系統

    /// <summary>
    /// 派工單
    /// </summary>
    public DbSet<WorkOrder> WorkOrders { get; set; }

    /// <summary>
    /// 派工單附圖
    /// </summary>
    public DbSet<WorkOrderImage> WorkOrderImages { get; set; }

    /// <summary>
    /// 派工單指派事務機（多對多）
    /// </summary>
    public DbSet<WorkOrderPrinter> WorkOrderPrinters { get; set; }

    /// <summary>
    /// 派工單指派人員（多對多）
    /// </summary>
    public DbSet<WorkOrderAssignee> WorkOrderAssignees { get; set; }

    #endregion

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Partner
        modelBuilder.Entity<Partner>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.State);
        });

        // Brand
        modelBuilder.Entity<Brand>(entity =>
        {
            entity.HasIndex(e => e.Name);
        });

        // PrinterModel
        modelBuilder.Entity<PrinterModel>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.State);

            entity.HasOne(e => e.Brand)
                .WithMany(b => b.Models)
                .HasForeignKey(e => e.BrandId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // PartnerContact
        modelBuilder.Entity<PartnerContact>(entity =>
        {
            entity.HasIndex(e => e.PartnerId);

            entity.HasOne(e => e.Partner)
                .WithMany(p => p.Contacts)
                .HasForeignKey(e => e.PartnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PartnerBillingInfo
        modelBuilder.Entity<PartnerBillingInfo>(entity =>
        {
            entity.HasIndex(e => e.PartnerId);

            entity.HasOne(e => e.Partner)
                .WithMany(p => p.BillingInfos)
                .HasForeignKey(e => e.PartnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Printer
        modelBuilder.Entity<Printer>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.SerialNumber);
            entity.HasIndex(e => e.ContractEndDate);

            entity.HasOne(e => e.Partner)
                .WithMany(p => p.Printers)
                .HasForeignKey(e => e.PartnerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Model)
                .WithMany(m => m.Printers)
                .HasForeignKey(e => e.ModelId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.BillingGroup)
                .WithMany(g => g.Members)
                .HasForeignKey(e => e.BillingGroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // PrintRecord
        modelBuilder.Entity<PrintRecord>(entity =>
        {
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => new { e.PrinterId, e.Date, e.State });

            entity.HasOne(e => e.Printer)
                .WithMany(p => p.PrintRecords)
                .HasForeignKey(e => e.PrinterId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Partner)
                .WithMany()
                .HasForeignKey(e => e.PartnerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // AlertRecord
        modelBuilder.Entity<AlertRecord>(entity =>
        {
            entity.HasIndex(e => e.State);
            entity.HasIndex(e => new { e.PrinterId, e.State });

            entity.HasOne(e => e.Printer)
                .WithMany(p => p.AlertRecords)
                .HasForeignKey(e => e.PrinterId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AppUser
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Role);
        });

        // RolePermission
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasIndex(e => new { e.Role, e.FeatureCode }).IsUnique();
        });

        // EinvoicePlatform
        modelBuilder.Entity<EinvoicePlatform>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // EinvoiceFieldMapping
        modelBuilder.Entity<EinvoiceFieldMapping>(entity =>
        {
            entity.HasIndex(e => new { e.PlatformId, e.FieldCode }).IsUnique();

            entity.HasOne(e => e.Platform)
                .WithMany(p => p.FieldMappings)
                .HasForeignKey(e => e.PlatformId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Einvoice
        modelBuilder.Entity<Einvoice>(entity =>
        {
            entity.HasIndex(e => e.InvoiceNumber);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.InvoiceDate);

            entity.HasOne(e => e.Partner)
                .WithMany()
                .HasForeignKey(e => e.PartnerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Platform)
                .WithMany()
                .HasForeignKey(e => e.PlatformId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.BillingInvoice)
                .WithMany()
                .HasForeignKey(e => e.BillingInvoiceId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // EinvoiceItem
        modelBuilder.Entity<EinvoiceItem>(entity =>
        {
            entity.HasOne(e => e.Einvoice)
                .WithMany(i => i.Items)
                .HasForeignKey(e => e.EinvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PrinterMaintainer
        modelBuilder.Entity<PrinterMaintainer>(entity =>
        {
            entity.HasIndex(e => new { e.PrinterId, e.UserId }).IsUnique();

            entity.HasOne(e => e.Printer)
                .WithMany(p => p.Maintainers)
                .HasForeignKey(e => e.PrinterId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        #region 模組化計費系統

        // SystemModule
        modelBuilder.Entity<SystemModule>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.SortOrder);
        });

        // PartnerModule
        modelBuilder.Entity<PartnerModule>(entity =>
        {
            entity.HasIndex(e => new { e.PartnerId, e.ModuleId }).IsUnique();

            entity.HasOne(e => e.Partner)
                .WithMany()
                .HasForeignKey(e => e.PartnerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Module)
                .WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PrinterBillingConfig
        modelBuilder.Entity<PrinterBillingConfig>(entity =>
        {
            entity.HasIndex(e => e.PrinterId).IsUnique();

            entity.HasOne(e => e.Printer)
                .WithOne()
                .HasForeignKey<PrinterBillingConfig>(e => e.PrinterId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Invoice
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasIndex(e => e.InvoiceNumber).IsUnique();
            entity.HasIndex(e => new { e.PartnerId, e.BillingPeriod });
            entity.HasIndex(e => e.Status);

            entity.HasOne(e => e.Partner)
                .WithMany()
                .HasForeignKey(e => e.PartnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // InvoiceItem
        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasIndex(e => new { e.InvoiceId, e.PrinterId });

            entity.HasOne(e => e.Invoice)
                .WithMany(i => i.Items)
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Printer)
                .WithMany()
                .HasForeignKey(e => e.PrinterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.BillingGroup)
                .WithMany()
                .HasForeignKey(e => e.BillingGroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // PrinterBillingGroup
        modelBuilder.Entity<PrinterBillingGroup>(entity =>
        {
            entity.HasIndex(e => e.BillingPartnerId);
            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.BillingPartner)
                .WithMany()
                .HasForeignKey(e => e.BillingPartnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // BillingGroupSheetPrice
        modelBuilder.Entity<BillingGroupSheetPrice>(entity =>
        {
            entity.HasIndex(e => new { e.GroupId, e.SheetTypeId }).IsUnique();

            entity.HasOne(e => e.Group)
                .WithMany(g => g.SheetPrices)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SheetType)
                .WithMany()
                .HasForeignKey(e => e.SheetTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // BillingGroupSheetTier
        modelBuilder.Entity<BillingGroupSheetTier>(entity =>
        {
            entity.HasIndex(e => new { e.GroupId, e.SheetTypeId, e.TierOrder });

            entity.HasOne(e => e.Group)
                .WithMany(g => g.SheetTiers)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SheetType)
                .WithMany()
                .HasForeignKey(e => e.SheetTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // BillingTemplate
        modelBuilder.Entity<BillingTemplate>(entity =>
        {
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.IsActive);
        });

        // BillingTier
        modelBuilder.Entity<BillingTier>(entity =>
        {
            entity.HasIndex(e => new { e.BillingConfigId, e.TierType, e.TierOrder });
            entity.HasIndex(e => new { e.BillingTemplateId, e.TierType, e.TierOrder });

            entity.HasOne(e => e.BillingConfig)
                .WithMany(c => c.Tiers)
                .HasForeignKey(e => e.BillingConfigId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.BillingTemplate)
                .WithMany(t => t.Tiers)
                .HasForeignKey(e => e.BillingTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // InvoicePrintSettings
        modelBuilder.Entity<InvoicePrintSettings>(entity =>
        {
            entity.HasIndex(e => e.TemplateCode);
        });

        #endregion

        #region 彈性張數類型系統

        // SheetType
        modelBuilder.Entity<SheetType>(entity =>
        {
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.SortOrder);
        });

        // SheetTypeKey
        modelBuilder.Entity<SheetTypeKey>(entity =>
        {
            entity.HasIndex(e => e.DriverKey);
            entity.HasIndex(e => e.SheetTypeId);

            entity.HasOne(e => e.SheetType)
                .WithMany(t => t.Keys)
                .HasForeignKey(e => e.SheetTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PrinterModelSheetType (junction)
        modelBuilder.Entity<PrinterModelSheetType>(entity =>
        {
            entity.HasIndex(e => new { e.PrinterModelId, e.SheetTypeId }).IsUnique();

            entity.HasOne(e => e.PrinterModel)
                .WithMany(m => m.PrinterModelSheetTypes)
                .HasForeignKey(e => e.PrinterModelId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SheetType)
                .WithMany(s => s.PrinterModelSheetTypes)
                .HasForeignKey(e => e.SheetTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PrinterBillingSheetPrice
        modelBuilder.Entity<PrinterBillingSheetPrice>(entity =>
        {
            entity.HasIndex(e => new { e.PrinterId, e.SheetTypeId }).IsUnique();

            entity.HasOne(e => e.Printer)
                .WithMany()
                .HasForeignKey(e => e.PrinterId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SheetType)
                .WithMany(t => t.BillingPrices)
                .HasForeignKey(e => e.SheetTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PrinterBillingSheetTier
        modelBuilder.Entity<PrinterBillingSheetTier>(entity =>
        {
            entity.HasOne(e => e.Printer)
                .WithMany()
                .HasForeignKey(e => e.PrinterId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SheetType)
                .WithMany()
                .HasForeignKey(e => e.SheetTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PrintRecordValue
        modelBuilder.Entity<PrintRecordValue>(entity =>
        {
            entity.HasIndex(e => new { e.RecordId, e.SheetTypeId }).IsUnique();

            entity.HasOne(e => e.Record)
                .WithMany(r => r!.Values)
                .HasForeignKey(e => e.RecordId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SheetType)
                .WithMany(t => t.RecordValues)
                .HasForeignKey(e => e.SheetTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // SheetTypeConversionRate
        modelBuilder.Entity<SheetTypeConversionRate>(entity =>
        {
            entity.HasIndex(e => new { e.FromSheetTypeId, e.ToSheetTypeId }).IsUnique();
            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.FromSheetType)
                .WithMany()
                .HasForeignKey(e => e.FromSheetTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ToSheetType)
                .WithMany()
                .HasForeignKey(e => e.ToSheetTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        #endregion

        #region 派工系統

        // WorkOrder
        modelBuilder.Entity<WorkOrder>(entity =>
        {
            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.PartnerId);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasOne(e => e.Partner)
                .WithMany()
                .HasForeignKey(e => e.PartnerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // WorkOrderImage
        modelBuilder.Entity<WorkOrderImage>(entity =>
        {
            entity.HasIndex(e => e.WorkOrderId);

            entity.HasOne(e => e.WorkOrder)
                .WithMany(w => w.Images)
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // WorkOrderPrinter (派工單 ↔ 事務機)
        modelBuilder.Entity<WorkOrderPrinter>(entity =>
        {
            entity.HasIndex(e => new { e.WorkOrderId, e.PrinterId }).IsUnique();

            entity.HasOne(e => e.WorkOrder)
                .WithMany(w => w.WorkOrderPrinters)
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Printer)
                .WithMany()
                .HasForeignKey(e => e.PrinterId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // WorkOrderAssignee (派工單 ↔ 人員)
        modelBuilder.Entity<WorkOrderAssignee>(entity =>
        {
            entity.HasIndex(e => new { e.WorkOrderId, e.UserId }).IsUnique();

            entity.HasOne(e => e.WorkOrder)
                .WithMany(w => w.WorkOrderAssignees)
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        #endregion
    }
}

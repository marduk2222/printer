using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Data.Entities;
using printer.Models.Dto;

namespace printer.Services.Impl;

/// <summary>
/// 印表機服務實作
/// </summary>
public class PrinterService : IPrinterService
{
    private readonly PrinterDbContext _context;
    private readonly ILogger<PrinterService> _logger;

    public PrinterService(PrinterDbContext context, ILogger<PrinterService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<PrinterDataDto>> GetPrintersAsync(PrinterListRequest request)
    {
        var query = _context.Printers
            .Include(p => p.Partner)
            .Include(p => p.Model)
            .Where(p => p.IsActive);

        if (!string.IsNullOrEmpty(request.PartnerId))
        {
            var partner = await _context.Partners
                .FirstOrDefaultAsync(p => p.Code == request.PartnerId);
            if (partner != null)
            {
                query = query.Where(p => p.PartnerId == partner.Id);
            }
        }
        else if (request.UserId.HasValue)
        {
            query = query.Where(p => p.UserId == request.UserId);
        }

        var printers = await query.ToListAsync();

        return printers.Select(p => new PrinterDataDto
        {
            Id = p.Id,
            Code = p.Code,
            PartnerId = p.PartnerId,
            UserId = p.UserId,
            ModelId = int.TryParse(p.Model?.Code, out var _mc) ? _mc : 0,
            Name = p.Name,
            Description = p.Description ?? "",
            IsActive = p.IsActive,
            Number = p.PartnerNumber ?? "",
            SerialNumber = p.SerialNumber ?? "",
            Ip = p.Ip ?? "",
            Mac = p.Mac ?? ""
        }).ToList();
    }

    public async Task<List<PrinterDataDto>> GetPeriodPrintersAsync(PeriodRequest request)
    {
        var query = _context.Printers
            .Include(p => p.Partner)
            .Include(p => p.Model)
            .Where(p => p.IsActive);

        if (!string.IsNullOrEmpty(request.PartnerId))
        {
            var partner = await _context.Partners
                .FirstOrDefaultAsync(p => p.IsCompany && p.Code == request.PartnerId);
            if (partner != null)
            {
                query = query.Where(p => p.PartnerId == partner.Id);
            }
        }

        var printers = await query.ToListAsync();

        return printers.Select(p => new PrinterDataDto
        {
            Id = p.Id,
            Code = p.Code,
            PartnerId = p.PartnerId,
            IsActive = p.IsActive,
            Number = p.PartnerNumber ?? "",
            ModelId = int.TryParse(p.Model?.Code, out var _mc) ? _mc : 0,
            Name = p.Name,
            Description = p.Description ?? "",
            PrinterName = p.PrinterName ?? "",
            SerialNumber = p.SerialNumber ?? "",
            Ip = p.Ip ?? "",
            Mac = p.Mac ?? ""
        }).ToList();
    }

    public async Task<SuppliesUpdateResponse?> UpdateSuppliesAsync(SuppliesUpdateRequest request)
    {
        var printer = await _context.Printers
            .FirstOrDefaultAsync(p => p.Code == request.Code);

        if (printer == null)
            return null;

        printer.TonerBlack = request.TonerBlack;
        printer.TonerCyan = request.TonerCyan;
        printer.TonerMagenta = request.TonerMagenta;
        printer.TonerYellow = request.TonerYellow;
        printer.TonerWaste = request.TonerWaste;
        printer.DrumBlack = request.DrumBlack;
        printer.DrumCyan = request.DrumCyan;
        printer.DrumMagenta = request.DrumMagenta;
        printer.DrumYellow = request.DrumYellow;
        printer.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated supplies for printer {Code}", request.Code);

        return new SuppliesUpdateResponse
        {
            Id = printer.Id,
            Code = printer.Code
        };
    }

    public async Task<AlertsUpdateResponse?> UpdateAlertsAsync(AlertsUpdateRequest request)
    {
        var printer = await _context.Printers
            .FirstOrDefaultAsync(p => p.Code == request.Code);

        if (printer == null)
            return null;

        // 刪除舊的 alert 記錄
        var oldAlerts = await _context.AlertRecords
            .Where(a => a.PrinterId == printer.Id)
            .ToListAsync();
        _context.AlertRecords.RemoveRange(oldAlerts);

        // 建立新的 alert 記錄
        foreach (var alertCode in request.Alerts)
        {
            _context.AlertRecords.Add(new AlertRecord
            {
                PrinterId = printer.Id,
                Code = alertCode,
                State = "warn",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        return new AlertsUpdateResponse
        {
            Id = printer.Id,
            Code = printer.Code,
            Count = request.Alerts.Count
        };
    }

    public async Task<(RecordResponse? response, string? error)> WriteRecordAsync(RecordRequest request)
    {
        var printer = await _context.Printers
            .FirstOrDefaultAsync(p => p.Code == request.Code && p.IsActive);

        if (printer == null)
            return (null, "printer not found or not in use");

        var date = DateOnly.Parse(request.Date);

        // 查找當日記錄
        var record = await _context.PrintRecords
            .FirstOrDefaultAsync(r =>
                r.PrinterId == printer.Id &&
                r.Date == date &&
                r.State == "auto");

        if (record != null)
        {
            // 更新現有記錄
            record.BlackSheets = request.BlackPrint ?? 0;
            record.ColorSheets = request.ColorPrint ?? 0;
            record.LargeSheets = request.LargePrint ?? 0;
            record.Count++;
            record.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return (new RecordResponse
            {
                Id = printer.Id,
                Code = printer.Code,
                RecordId = record.Id
            }, null);
        }
        else
        {
            // 建立新記錄
            var newRecord = new PrintRecord
            {
                PrinterId = printer.Id,
                PartnerId = printer.PartnerId,
                Date = date,
                BlackSheets = request.BlackPrint ?? 0,
                ColorSheets = request.ColorPrint ?? 0,
                LargeSheets = request.LargePrint ?? 0,
                State = "auto",
                Count = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.PrintRecords.Add(newRecord);
            await _context.SaveChangesAsync();

            return (new RecordResponse
            {
                Id = printer.Id,
                Code = printer.Code,
                RecordId = newRecord.Id
            }, null);
        }
    }

    public async Task<DeviceUpdateResponse?> UpdateDeviceAsync(DeviceUpdateRequest request)
    {
        var printer = await _context.Printers
            .FirstOrDefaultAsync(p => p.Code == request.Code);

        if (printer == null)
            return null;

        if (!string.IsNullOrEmpty(request.Mac))
            printer.Mac = request.Mac;
        if (!string.IsNullOrEmpty(request.Ip))
            printer.Ip = request.Ip;
        if (!string.IsNullOrEmpty(request.SerialNumber))
            printer.SerialNumber = request.SerialNumber;
        if (!string.IsNullOrEmpty(request.PrinterName))
            printer.PrinterName = request.PrinterName;
        if (request.IsActive.HasValue)
            printer.IsActive = request.IsActive.Value;

        printer.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new DeviceUpdateResponse
        {
            Id = printer.Id,
            Code = printer.Code
        };
    }

    public async Task<(InstallResponse? response, string? error)> CreateInstallAsync(InstallRequest request)
    {
        var printer = await _context.Printers
            .FirstOrDefaultAsync(p => p.Code == request.Code);

        if (printer == null)
            return (null, "printer not found");

        // 建立安裝記錄 (使用 PrintRecord 作為安裝記錄)
        var date = string.IsNullOrEmpty(request.Date)
            ? DateOnly.FromDateTime(DateTime.Today)
            : DateOnly.Parse(request.Date);

        var record = new PrintRecord
        {
            PrinterId = printer.Id,
            PartnerId = printer.PartnerId,
            Date = date,
            BlackSheets = 0,
            ColorSheets = 0,
            LargeSheets = 0,
            State = $"install_{request.State}",
            Count = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.PrintRecords.Add(record);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created install record for printer {Code}", request.Code);

        return (new InstallResponse
        {
            Id = printer.Id,
            Code = printer.Code
        }, null);
    }
}

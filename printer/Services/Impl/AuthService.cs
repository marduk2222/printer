using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using printer.Data;
using printer.Models.Dto;

namespace printer.Services.Impl;

/// <summary>
/// 認證服務實作
/// </summary>
public class AuthService : IAuthService
{
    private readonly PrinterDbContext _context;
    private readonly ILogger<AuthService> _logger;

    public AuthService(PrinterDbContext context, ILogger<AuthService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(AuthResponse? response, string? error)> VerifyStaffAsync(AuthRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Account) || string.IsNullOrWhiteSpace(request.Password))
            return (null, "missing account or password");

        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(request.Password)));
        var user = await _context.AppUsers
            .FirstOrDefaultAsync(u => u.Username == request.Account && u.PasswordHash == hash && u.IsActive);

        if (user == null)
        {
            _logger.LogInformation("Verify staff failed: {Account}", request.Account);
            return (null, "invalid credentials");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (new AuthResponse
        {
            Id = user.Id,
            Name = user.DisplayName,
            Mobile = user.Phone ?? string.Empty
        }, null);
    }

    public async Task<List<CompanyDto>> GetCompaniesAsync(CompanyRequest request)
    {
        var query = _context.Partners.Where(p => p.IsCompany);

        if (!string.IsNullOrEmpty(request.Keyword))
        {
            var keyword = request.Keyword.ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(keyword) ||
                c.Code.ToLower().Contains(keyword));
        }

        var partners = await query.Take(50).ToListAsync();

        return partners.Select(p => new CompanyDto
        {
            Id = p.Id,
            Name = p.Name,
            Code = p.Code
        }).ToList();
    }
}

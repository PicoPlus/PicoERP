using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PicoERP.Application.Common;
using PicoERP.Application.DTOs;
using PicoERP.Application.Interfaces;
using PicoERP.Infrastructure.Persistence;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PicoERP.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == dto.Username && u.IsActive);
        if (user == null) return Result<LoginResponseDto>.Failure("نام کاربری یا رمز عبور اشتباه است");
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Result<LoginResponseDto>.Failure("نام کاربری یا رمز عبور اشتباه است");

        user.LastLoginAt = DateTime.UtcNow;
        var (token, expires) = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _db.SaveChangesAsync();

        return Result<LoginResponseDto>.Success(new LoginResponseDto
        {
            Token = token,
            RefreshToken = refreshToken,
            ExpiresAt = expires,
            User = new UserInfoDto
            {
                Id = user.Id, Username = user.Username,
                DisplayName = user.DisplayName,
                Role = user.Role.ToString(),
                EmployeeId = user.EmployeeId
            }
        });
    }

    public async Task<Result<LoginResponseDto>> RefreshTokenAsync(string refreshToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken && u.RefreshTokenExpiry > DateTime.UtcNow && u.IsActive);
        if (user == null) return Result<LoginResponseDto>.Failure("توکن نامعتبر است");

        var (token, expires) = GenerateJwtToken(user);
        var newRefresh = GenerateRefreshToken();
        user.RefreshToken = newRefresh;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _db.SaveChangesAsync();

        return Result<LoginResponseDto>.Success(new LoginResponseDto
        {
            Token = token, RefreshToken = newRefresh, ExpiresAt = expires,
            User = new UserInfoDto { Id = user.Id, Username = user.Username, DisplayName = user.DisplayName, Role = user.Role.ToString() }
        });
    }

    public async Task<Result> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Result.Failure("کاربر یافت نشد");
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            return Result.Failure("رمز عبور فعلی اشتباه است");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task LogoutAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user != null) { user.RefreshToken = null; user.RefreshTokenExpiry = null; await _db.SaveChangesAsync(); }
    }

    public async Task<UserInfoDto?> GetUserInfoAsync(int userId)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return null;
        return new UserInfoDto { Id = user.Id, Username = user.Username, DisplayName = user.DisplayName, Role = user.Role.ToString(), EmployeeId = user.EmployeeId };
    }

    private (string token, DateTime expires) GenerateJwtToken(Domain.Entities.AppUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? "PicoERP-Super-Secret-Key-2024!!"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(8);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("displayName", user.DisplayName),
            new Claim("employeeId", user.EmployeeId?.ToString() ?? "")
        };
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "PicoERP",
            audience: _config["Jwt:Audience"] ?? "PicoERP",
            claims: claims,
            expires: expires,
            signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}

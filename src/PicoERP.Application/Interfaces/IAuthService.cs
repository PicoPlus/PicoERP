using PicoERP.Application.Common;
using PicoERP.Application.DTOs;

namespace PicoERP.Application.Interfaces;

public interface IAuthService
{
    Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto dto);
    Task<Result<LoginResponseDto>> RefreshTokenAsync(string refreshToken);
    Task<Result> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
    Task LogoutAsync(int userId);
    Task<UserInfoDto?> GetUserInfoAsync(int userId);
}

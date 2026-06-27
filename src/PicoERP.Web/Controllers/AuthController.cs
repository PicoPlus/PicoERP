using Microsoft.AspNetCore.Mvc;
using PicoERP.Application.DTOs;
using PicoERP.Application.Interfaces;
using PicoERP.Web.Filters;

namespace PicoERP.Web.Controllers;

/// <summary>
/// Mobile authentication endpoints.
/// All routes require a valid X-Mobile-Api-Key header (license check).
/// </summary>
[ApiController]
[Route("api/auth")]
[MobileApiKey]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Login with username + password → returns JWT + refresh token.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var result = await _auth.LoginAsync(dto);
        if (!result.IsSuccess)
            return Unauthorized(new { error = result.Error });
        return Ok(result.Data);
    }

    /// <summary>Exchange a refresh token for a new JWT pair.</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto dto)
    {
        var result = await _auth.RefreshTokenAsync(dto.RefreshToken);
        if (!result.IsSuccess)
            return Unauthorized(new { error = result.Error });
        return Ok(result.Data);
    }
}

/// <summary>Request model for token refresh.</summary>
public sealed class RefreshRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}

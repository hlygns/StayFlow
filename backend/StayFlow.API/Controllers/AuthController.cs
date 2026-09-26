using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StayFlow.API.Infrastructure;
using StayFlow.Application.Auth;

namespace StayFlow.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    /// <summary>Refresh token'in tasindigi cookie.</summary>
    public const string RefreshCookieName = "stayflow_refresh";

    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Yeni otel + otel sahibi hesabi olusturur ve direkt giris yapmis sayar.</summary>
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterHotelRequest request, CancellationToken ct) =>
        WithRefreshCookie(await _authService.RegisterHotelAsync(request, ct));

    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        WithRefreshCookie(await _authService.LoginAsync(request, ct));

    /// <summary>Govde yok: refresh token tarayicinin otomatik gonderdigi httpOnly cookie'den okunur.</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        try
        {
            return WithRefreshCookie(await _authService.RefreshAsync(Request.Cookies[RefreshCookieName], ct));
        }
        catch
        {
            // Gecersiz/suresi dolmus cookie tarayicida kalmasin
            Response.Cookies.Delete(RefreshCookieName, CookieOptions(DateTimeOffset.UnixEpoch));
            throw;
        }
    }

    /// <summary>Access token suresi dolmus olsa bile cikis yapilabilsin diye [Authorize] yok: cookie yeterli.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _authService.LogoutAsync(Request.Cookies[RefreshCookieName], ct);
        Response.Cookies.Delete(RefreshCookieName, CookieOptions(DateTimeOffset.UnixEpoch));
        return NoContent();
    }

    private ActionResult<AuthResponse> WithRefreshCookie(AuthResult result)
    {
        Response.Cookies.Append(RefreshCookieName, result.RefreshToken, CookieOptions(result.RefreshTokenExpiresAt));
        return Ok(result.Response);
    }

    /// <summary>
    /// HttpOnly: JavaScript okuyamaz (XSS ile calinamaz).
    /// Secure: sadece HTTPS ile gonderilir (tarayicilar localhost'u istisna sayar).
    /// SameSite=Strict + Path=/api/auth: baska sitelerden gelen isteklere ve diger API uclarina eklenmez (CSRF).
    /// </summary>
    private static CookieOptions CookieOptions(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        Expires = expires,
        IsEssential = true
    };
}
